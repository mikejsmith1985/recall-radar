// Checks a search can be confined to the owner's own version of a model, against a real database.
using Microsoft.EntityFrameworkCore;
using RecallRadar.Domain.Retrieval;
using RecallRadar.Domain.Vehicles;
using RecallRadar.Retrieval.Embeddings;
using RecallRadar.Retrieval.Persistence;
using RecallRadar.Retrieval.Search;

namespace RecallRadar.Integration.Search;

/// <summary>
/// NHTSA files every version of a model under one name: a 2023 F-150 covers a 2.7 litre V6 and the
/// Raptor R's supercharged 5.2 V8 alike. Answering about the Raptor R from that whole pool is
/// answering about a different truck, which is what these tests hold the line on.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class TrimScopedSearchTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string Query = "transmission shifts harshly";
    private const string Component = "POWER TRAIN";
    private static readonly VehicleFit RaptorR = VehicleFit.Create("SuperCrew-Raptor", 5.2m, 8);

    private int _vehicleId;

    public async ValueTask InitializeAsync()
    {
        await using var context = postgres.CreateContext();
        var vehicle = Vehicle.Create(
            "FORD", $"TRIMTEST-{Guid.NewGuid():N}", 2023, $"Trim fixture {Guid.NewGuid():N}",
            vin: "1FTFW1RJ0PFB00000");
        vehicle.DescribeFit(RaptorR);
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _vehicleId = vehicle.Id;

        await SeedAsync(context, "mine", RaptorR, "The transmission shifts harshly on my Raptor R.");
        await SeedAsync(context, "other-engine", VehicleFit.Create("SuperCrew", 2.7m, 6),
            "The transmission shifts harshly under load.");
        await SeedAsync(context, "same-trim-other-engine", VehicleFit.Create("SuperCrew-Raptor", 3.5m, 6),
            "The transmission shifts harshly when towing.");
        await SeedAsync(context, "undecoded", VehicleFit.Unknown,
            "The transmission shifts harshly and the dealer found nothing.");
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task ThisTrim_LeavesOutARecordFromAnotherEngine()
    {
        // The whole point: a 2.7 V6's transmission complaint is not about a supercharged V8.
        var externalIds = await SearchAsync(TrimScope.ThisTrim);

        Assert.Contains("mine", externalIds);
        Assert.DoesNotContain("other-engine", externalIds);
    }

    [Fact]
    public async Task ThisTrim_LeavesOutTheSameTrimWithADifferentEngine()
    {
        // The 3.5 EcoBoost Raptor and the 5.2 Raptor R share a trim name and no engine.
        Assert.DoesNotContain("same-trim-other-engine", await SearchAsync(TrimScope.ThisTrim));
    }

    [Fact]
    public async Task ThisTrim_KeepsARecordNhtsaCouldNotDecode()
    {
        // A quarter of the corpus decodes to blanks. Discarding it would lose more than it saves.
        Assert.Contains("undecoded", await SearchAsync(TrimScope.ThisTrim));
    }

    [Fact]
    public async Task AllTrims_KeepsEverythingTheVehicleHolds()
    {
        var externalIds = await SearchAsync(TrimScope.AllTrims);

        Assert.Contains("mine", externalIds);
        Assert.Contains("other-engine", externalIds);
        Assert.Contains("same-trim-other-engine", externalIds);
    }

    [Fact]
    public async Task AVehicleWithNoDecodedVinNarrowsNothing()
    {
        // Somebody who never gave a VIN must not silently lose records to a filter that cannot run.
        await using var context = postgres.CreateContext();
        var vehicle = await context.Vehicles.SingleAsync(
            candidate => candidate.Id == _vehicleId, TestContext.Current.CancellationToken);
        vehicle.DescribeFit(VehicleFit.Unknown);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        var externalIds = await SearchAsync(TrimScope.ThisTrim);

        Assert.Contains("other-engine", externalIds);
    }

    [Fact]
    public async Task CountOtherTrims_SaysHowManyWereSetAside()
    {
        // The number is what lets the page offer the wider search instead of an empty page.
        await using var context = postgres.CreateContext();
        var request = BuildRequest(TrimScope.ThisTrim);

        var setAside = await BuildService(context).CountOtherTrimsAsync(request, TestContext.Current.CancellationToken);

        Assert.Equal(2, setAside);
    }

    [Fact]
    public async Task CountOtherTrims_MeansTheSameThingWhicheverScopeRan()
    {
        // "How many belong to another version" is a fact about the vehicle, not about the search.
        // A number that meant something different under each scope could not be read without
        // knowing which scope produced it.
        await using var context = postgres.CreateContext();

        Assert.Equal(
            2,
            await BuildService(context).CountOtherTrimsAsync(
                BuildRequest(TrimScope.AllTrims), TestContext.Current.CancellationToken));
    }

    private async Task<IReadOnlyList<string>> SearchAsync(TrimScope trims)
    {
        await using var context = postgres.CreateContext();
        var hits = await BuildService(context).SearchAsync(BuildRequest(trims), TestContext.Current.CancellationToken);
        return [.. hits.Select(hit => hit.ExternalId)];
    }

    private SearchRequest BuildRequest(TrimScope trims)
    {
        var wasBuilt = SearchRequest.TryCreate(
            _vehicleId, Query, "sparse", null, null, null, SearchRequest.MaximumLimit, null,
            trims.ToString(), out var request, out var problem);
        Assert.True(wasBuilt, problem);
        return request!;
    }

    private static HybridSearchService BuildService(RecallRadarDbContext context) =>
        new(context, new DeterministicEmbeddingGenerator());

    private async Task SeedAsync(RecallRadarDbContext context, string externalId, VehicleFit fit, string body)
    {
        var document = SourceDocument.Create(
            SourceKind.Complaint, externalId, _vehicleId, Component, new DateOnly(2023, 6, 1), externalId, body, "{}");
        document.DescribeFit(fit.IsKnown ? "1FTFW1RJ" : null, fit);
        context.SourceDocuments.Add(document);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        context.DocumentChunks.Add(DocumentChunk.Create(document.Id, 0, body));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
