// Checks that the evaluation scores every mode over identical cases and reproduces its numbers.
using System.Text.Json;
using Microsoft.Extensions.AI;
using Microsoft.EntityFrameworkCore;
using RecallRadar.Retrieval.Embeddings;
using RecallRadar.Retrieval.Evaluation;
using RecallRadar.Retrieval.Persistence;
using RecallRadar.Retrieval.Search;

namespace RecallRadar.Integration.Evaluation;

[Collection(PostgresCollection.Name)]
public sealed class EvaluationRunnerTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string Component = "SERVICE BRAKES, HYDRAULIC:FOUNDATION";
    private const string Campaign = "16V345000";
    private const string InvestigationBody = "Master cylinder external leak investigation into brake pedal travel.";
    private const string RecallBody = "Ford is recalling certain vehicles because the brake master cylinder may leak.";

    private static readonly DateOnly Opened = new(2016, 2, 29);
    private static readonly DateOnly Closed = new(2016, 7, 26);
    private static readonly DateTimeOffset FixedNow = new(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);

    private int _vehicleId;

    public async Task InitializeAsync()
    {
        await using var context = postgres.CreateContext();
        var vehicle = Vehicle.Create("FORD", $"EVAL-{Guid.NewGuid():N}", 2013, $"Eval fixture {Guid.NewGuid():N}");
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();
        _vehicleId = vehicle.Id;

        var investigationId = await SeedAsync(context, SourceKind.Investigation, "PE16003", InvestigationBody);
        await SeedAsync(context, SourceKind.Recall, Campaign, RecallBody);
        foreach (var index in Enumerable.Range(1, 6))
        {
            await SeedAsync(context, SourceKind.Complaint, $"c{index}",
                $"The brake pedal went to the floor and the master cylinder leaked, report {index}.");
        }

        context.InvestigationLinks.Add(InvestigationLink.Create(investigationId, Campaign, Component, Opened, Closed));
        await context.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task ARunScoresEveryModeAndRecordsTheCaseCount()
    {
        var result = await RunAsync();

        Assert.Equal(6, result.CaseCount);
        Assert.Equal(["sparse", "dense", "hybrid"], result.Modes.Select(mode => mode.Mode));
        Assert.All(result.Modes, mode => Assert.True(mode.WasScored));
    }

    [Fact]
    public async Task KeywordRetrievalFindsTheInvestigationItsQueriesCameFrom()
    {
        // The complaints are the ones the investigation was opened about, so retrieval that works
        // must surface it. A zero here would mean the retrieval is not doing its job at all.
        var result = await RunAsync();

        var sparse = result.Modes.Single(mode => mode.Mode == "sparse");
        Assert.True(sparse.Metrics!.RecallAt10 > 0);
        Assert.True(sparse.Metrics.MeanReciprocalRank > 0);
        Assert.Equal(6, sparse.Metrics.ScoredCaseCount);
    }

    [Fact]
    public async Task TwoRunsOverUnchangedDataProduceIdenticalNumbers()
    {
        var first = await RunAsync();
        var second = await RunAsync();

        Assert.Equal(EvaluationRunner.Serialise(first), EvaluationRunner.Serialise(second));
    }

    [Fact]
    public async Task EveryRunIsStoredSoTheNumbersCanBeComparedLater()
    {
        var result = await RunAsync();

        await using var context = postgres.CreateContext();
        var stored = await context.EvaluationRuns.AsNoTracking()
            .Where(run => run.VehicleId == _vehicleId)
            .OrderByDescending(run => run.Id)
            .FirstAsync();

        Assert.Equal(result.CaseCount, stored.CaseCount);
        Assert.Equal(FixedNow, stored.RanAt);
        using var metrics = JsonDocument.Parse(stored.MetricsJson);
        Assert.True(metrics.RootElement.TryGetProperty("sparse", out _));
    }

    [Fact]
    public async Task AModeNeedingEmbeddingsIsSkippedWithAReasonRatherThanScoredZero()
    {
        // Zero would read as "hybrid is bad" when it means "hybrid was never tried".
        var result = await RunAsync(new NullEmbeddingGenerator());

        var sparse = result.Modes.Single(mode => mode.Mode == "sparse");
        var dense = result.Modes.Single(mode => mode.Mode == "dense");
        Assert.True(sparse.WasScored);
        Assert.False(dense.WasScored);
        Assert.NotNull(dense.SkippedReason);
        Assert.Null(dense.Metrics);
    }

    [Fact]
    public async Task AVehicleWithNoInvestigationsProducesNoCasesAndSaysWhy()
    {
        await using var context = postgres.CreateContext();
        var bare = Vehicle.Create("FORD", $"BARE-{Guid.NewGuid():N}", 2015, $"Bare eval fixture {Guid.NewGuid():N}");
        context.Vehicles.Add(bare);
        await context.SaveChangesAsync();

        var result = await BuildRunner(context).RunAsync(bare.Id, CancellationToken.None);

        Assert.Equal(0, result.CaseCount);
        Assert.All(result.Modes, mode => Assert.False(mode.WasScored));
        Assert.All(result.Modes, mode => Assert.Contains("ground-truth", mode.SkippedReason!, StringComparison.OrdinalIgnoreCase));
    }

    private async Task<EvaluationResult> RunAsync(IEmbeddingGenerator<string, Embedding<float>>? generator = null)
    {
        await using var context = postgres.CreateContext();
        return await BuildRunner(context, generator).RunAsync(_vehicleId, CancellationToken.None);
    }

    private static EvaluationRunner BuildRunner(
        RecallRadarDbContext context, IEmbeddingGenerator<string, Embedding<float>>? generator = null) =>
        new(new GroundTruthBuilder(context),
            new HybridSearchService(context, generator ?? new DeterministicEmbeddingGenerator()),
            context,
            new FixedClock(FixedNow));

    private async Task<long> SeedAsync(RecallRadarDbContext context, SourceKind kind, string externalId, string body)
    {
        var document = SourceDocument.Create(
            kind, externalId, _vehicleId, Component, new DateOnly(2016, 4, 1), externalId, body, "{}");
        context.SourceDocuments.Add(document);
        await context.SaveChangesAsync();
        var chunk = DocumentChunk.Create(document.Id, 0, body);
        var generated = await new DeterministicEmbeddingGenerator()
            .GenerateAsync([body], options: null, CancellationToken.None);
        chunk.SetEmbedding(new Pgvector.Vector(generated[0].Vector));
        context.DocumentChunks.Add(chunk);
        await context.SaveChangesAsync();
        return document.Id;
    }

    /// <summary>A clock that never moves, so a stored run's timestamp is assertable.</summary>
    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
