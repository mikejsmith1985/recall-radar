// Exercises the three retrieval paths against a real pgvector container with seeded records.
using Microsoft.EntityFrameworkCore;
using RecallRadar.Domain.Retrieval;
using RecallRadar.Retrieval.Embeddings;
using RecallRadar.Retrieval.Persistence;
using RecallRadar.Retrieval.Search;

namespace RecallRadar.Integration.Search;

[Collection(PostgresCollection.Name)]
public sealed class HybridSearchServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string ExhaustQuery = "exhaust odor cabin";
    private const string ExhaustComponent = "ENGINE AND ENGINE COOLING";
    private const string BrakeComponent = "SERVICE BRAKES";

    private int _vehicleId;
    private int _otherVehicleId;
    private readonly Dictionary<string, long> _documentIdsByExternalId = [];

    public async ValueTask InitializeAsync()
    {
        await using var context = postgres.CreateContext();
        var vehicle = Vehicle.Create("FORD", $"SEARCHTEST-{Guid.NewGuid():N}", 2013, $"Search fixture {Guid.NewGuid():N}");
        var otherVehicle = Vehicle.Create("FORD", $"OTHER-{Guid.NewGuid():N}", 2014, $"Other fixture {Guid.NewGuid():N}");
        context.Vehicles.AddRange(vehicle, otherVehicle);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _vehicleId = vehicle.Id;
        _otherVehicleId = otherVehicle.Id;

        await SeedAsync(context, "exhaust-1", ExhaustComponent, new DateOnly(2016, 5, 1),
            "A strong exhaust odor enters the cabin when accelerating hard.");
        await SeedAsync(context, "exhaust-2", ExhaustComponent, new DateOnly(2019, 5, 1),
            "Exhaust smell in the passenger compartment, worse with the rear hatch open.");
        await SeedAsync(context, "brakes-1", BrakeComponent, new DateOnly(2016, 6, 1),
            "The brake pedal went to the floor and the master cylinder leaked.");
        await SeedAsync(context, "other-vehicle-exhaust", ExhaustComponent, new DateOnly(2016, 5, 1),
            "Exhaust odor cabin complaint belonging to a different truck.", _otherVehicleId);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Sparse_FindsKeywordMatchesAndNeverCrossesVehicles()
    {
        var hits = await SearchAsync(RetrievalMode.Sparse, ExhaustQuery);

        Assert.NotEmpty(hits);
        Assert.All(hits, hit => Assert.NotNull(hit.Explanation.SparseRank));
        Assert.All(hits, hit => Assert.Null(hit.Explanation.DenseRank));
        Assert.Contains(hits, hit => hit.ExternalId == "exhaust-1");
        Assert.DoesNotContain(hits, hit => hit.ExternalId == "other-vehicle-exhaust");
        Assert.DoesNotContain(hits, hit => hit.ExternalId == "brakes-1");
    }

    [Fact]
    public async Task Sparse_RanksInDescendingOrderWithoutGapsInItsRanks()
    {
        var hits = await SearchAsync(RetrievalMode.Sparse, "exhaust");

        var ranks = hits.Select(hit => hit.Explanation.SparseRank!.Value).ToList();
        Assert.Equal(Enumerable.Range(1, ranks.Count), ranks);
    }

    [Fact]
    public async Task Dense_OrdersByEmbeddingDistanceAndReportsOnlyItsOwnRank()
    {
        var hits = await SearchAsync(RetrievalMode.Dense, ExhaustQuery);

        Assert.NotEmpty(hits);
        Assert.All(hits, hit => Assert.NotNull(hit.Explanation.DenseRank));
        Assert.All(hits, hit => Assert.Null(hit.Explanation.SparseRank));
        Assert.DoesNotContain(hits, hit => hit.ExternalId == "other-vehicle-exhaust");
    }

    [Fact]
    public async Task Hybrid_CarriesBothRanksForAnythingBothMethodsFound()
    {
        var hits = await SearchAsync(RetrievalMode.Hybrid, ExhaustQuery);

        Assert.NotEmpty(hits);
        Assert.Contains(hits, hit => hit.Explanation.WasFoundByBoth);
        Assert.All(hits, hit => Assert.True(hit.Explanation.FusedScore > 0));
        var scores = hits.Select(hit => hit.Explanation.FusedScore).ToList();
        Assert.Equal(scores.OrderByDescending(score => score), scores);
    }

    [Fact]
    public async Task ComponentFilter_RestrictsResultsToThatComponent()
    {
        // websearch_to_tsquery ANDs bare terms, so the query must be words the brake record holds.
        var unfiltered = await SearchAsync(RetrievalMode.Sparse, "brake pedal");
        var filtered = await SearchAsync(RetrievalMode.Sparse, "brake pedal", component: BrakeComponent);

        Assert.NotEmpty(filtered);
        Assert.All(filtered, hit => Assert.Equal(BrakeComponent, hit.Component));
        Assert.All(unfiltered, hit => Assert.Equal(BrakeComponent, hit.Component));
    }

    [Fact]
    public async Task DateFilter_RestrictsResultsToTheWindow()
    {
        var hits = await SearchAsync(
            RetrievalMode.Sparse, "exhaust", filedFrom: new DateOnly(2018, 1, 1), filedTo: new DateOnly(2020, 1, 1));

        Assert.NotEmpty(hits);
        Assert.All(hits, hit => Assert.Equal("exhaust-2", hit.ExternalId));
    }

    [Fact]
    public async Task Limit_CapsHowManyHitsComeBack()
    {
        var hits = await SearchAsync(RetrievalMode.Sparse, "exhaust", limit: 1);

        Assert.Single(hits);
    }

    [Fact]
    public async Task Dense_ThrowsWhenTheVehicleHasNoEmbeddedChunks()
    {
        await using var context = postgres.CreateContext();
        var bare = Vehicle.Create("FORD", $"BARE-{Guid.NewGuid():N}", 2015, $"Bare fixture {Guid.NewGuid():N}");
        context.Vehicles.Add(bare);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        await SeedAsync(context, "unembedded", ExhaustComponent, new DateOnly(2015, 1, 1),
            "Exhaust odor with no embedding stored.", bare.Id, withEmbedding: false);

        var service = BuildService(context);
        SearchRequest.TryCreate(bare.Id, ExhaustQuery, "dense", null, null, null, null, out var request, out _);

        await Assert.ThrowsAsync<EmbeddingsUnavailableException>(
            () => service.SearchAsync(request!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Search_ThrowsWhenTheVehicleWasNeverRegistered()
    {
        await using var context = postgres.CreateContext();
        var service = BuildService(context);
        SearchRequest.TryCreate(-1, ExhaustQuery, "sparse", null, null, null, null, out var request, out _);

        await Assert.ThrowsAsync<VehicleNotFoundException>(
            () => service.SearchAsync(request!, TestContext.Current.CancellationToken));
    }

    private async Task<IReadOnlyList<SearchHit>> SearchAsync(
        RetrievalMode mode, string query, string? component = null,
        DateOnly? filedFrom = null, DateOnly? filedTo = null, int? limit = null)
    {
        await using var context = postgres.CreateContext();
        SearchRequest.TryCreate(
            _vehicleId, query, mode.ToString(), component, filedFrom, filedTo, limit, out var request, out var problem);
        Assert.Null(problem);
        return await BuildService(context).SearchAsync(request!, TestContext.Current.CancellationToken);
    }

    private static HybridSearchService BuildService(RecallRadarDbContext context) =>
        new(context, new DeterministicEmbeddingGenerator());

    private async Task SeedAsync(
        RecallRadarDbContext context, string externalId, string component, DateOnly filedOn,
        string body, int? vehicleId = null, bool withEmbedding = true)
    {
        var document = SourceDocument.Create(
            SourceKind.Complaint, externalId, vehicleId ?? _vehicleId, component, filedOn, externalId, body, "{}");
        context.SourceDocuments.Add(document);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _documentIdsByExternalId[externalId] = document.Id;

        var chunk = DocumentChunk.Create(document.Id, 0, body);
        if (withEmbedding)
        {
            var generated = await new DeterministicEmbeddingGenerator()
                .GenerateAsync([body], options: null, TestContext.Current.CancellationToken);
            chunk.SetEmbedding(new Pgvector.Vector(generated[0].Vector));
        }

        context.DocumentChunks.Add(chunk);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
