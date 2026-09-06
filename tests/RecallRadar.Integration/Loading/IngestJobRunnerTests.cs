// Proves the background runner actually loads a vehicle, and records why when it cannot.
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RecallRadar.Api.Config;
using RecallRadar.Api.Endpoints;
using RecallRadar.Integration.Ingest;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Integration.Loading;

[Collection(PostgresCollection.Name)]
public sealed class IngestJobRunnerTests(PostgresFixture postgres) : IDisposable
{
    /// <summary>Long enough for a load against a local stub, short enough to fail fast if stuck.</summary>
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(60);

    /// <summary>Model years for this class only, well clear of the years other fixtures register.</summary>
    private const int FirstModelYear = 1960;

    private static int _yearOffset;

    private readonly NhtsaFixtureServer _nhtsa = new();

    public void Dispose() => _nhtsa.Dispose();

    [Fact]
    public async Task RegisteringAVehicleLoadsItsRecordsWithoutAnyoneRunningACommand()
    {
        // The whole point: adding a car in the app fills it, rather than leaving an empty vehicle
        // that only a command line can populate.
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var request = BuildRequest();

        var queued = await client.PostAsJsonAsync("/api/vehicles", request, TestContext.Current.CancellationToken);
        var load = await WaitForFinishAsync(client, await IdOfAsync(queued));

        Assert.Equal("succeeded", load.State);
        Assert.NotNull(load.VehicleId);
        Assert.Null(load.Message);

        await using var context = postgres.CreateContext();
        var storedCount = await context.SourceDocuments
            .CountAsync(document => document.VehicleId == load.VehicleId, TestContext.Current.CancellationToken);
        Assert.True(storedCount > 0, "the load reported success but stored no records");
    }

    [Fact]
    public async Task TheLoadReportsWhatItStored()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var queued = await client.PostAsJsonAsync("/api/vehicles", BuildRequest(), TestContext.Current.CancellationToken);
        var load = await WaitForFinishAsync(client, await IdOfAsync(queued));

        Assert.NotNull(load.Report);
        Assert.True(load.Report!.Value.GetProperty("complaintsNew").GetInt32() > 0);
        Assert.True(load.Report!.Value.GetProperty("chunksCreated").GetInt32() > 0);
    }

    [Fact]
    public async Task EveryStoredRecordIsRetrievableBecauseTheLoadChunkedIt()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var queued = await client.PostAsJsonAsync("/api/vehicles", BuildRequest(), TestContext.Current.CancellationToken);
        var load = await WaitForFinishAsync(client, await IdOfAsync(queued));

        await using var context = postgres.CreateContext();
        var documents = await context.SourceDocuments
            .CountAsync(document => document.VehicleId == load.VehicleId, TestContext.Current.CancellationToken);
        var chunked = await context.DocumentChunks
            .CountAsync(chunk => chunk.Document!.VehicleId == load.VehicleId, TestContext.Current.CancellationToken);

        Assert.Equal(documents, chunked);
    }

    [Fact]
    public async Task TheLoadedVehicleIsImmediatelySearchable()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var queued = await client.PostAsJsonAsync("/api/vehicles", BuildRequest(), TestContext.Current.CancellationToken);
        var load = await WaitForFinishAsync(client, await IdOfAsync(queued));

        var search = await client.GetAsync(
            $"/api/search?vehicleId={load.VehicleId}&q=exhaust%20odor%20cabin&mode=sparse",
            TestContext.Current.CancellationToken);

        Assert.True(search.IsSuccessStatusCode, await search.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task AFailedFeedIsRecordedOnTheJobRatherThanLostSilently()
    {
        // A job that vanished is indistinguishable from one still running, so the reason is stored.
        _nhtsa.FailRecallsFor("EXPLORER");
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var queued = await client.PostAsJsonAsync("/api/vehicles", BuildRequest(), TestContext.Current.CancellationToken);
        var load = await WaitForFinishAsync(client, await IdOfAsync(queued));

        Assert.Equal("failed", load.State);
        Assert.False(string.IsNullOrWhiteSpace(load.Message));
    }

    [Fact]
    public async Task AFailedLoadStoresNoRecordsAtAll()
    {
        // Ingestion fetches everything before opening its transaction, so a feed that dies part-way
        // leaves the database exactly as it was.
        _nhtsa.FailRecallsFor("EXPLORER");
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var request = BuildRequest();

        var queued = await client.PostAsJsonAsync("/api/vehicles", request, TestContext.Current.CancellationToken);
        await WaitForFinishAsync(client, await IdOfAsync(queued));

        await using var context = postgres.CreateContext();
        var vehicle = await context.Vehicles.FirstOrDefaultAsync(
            candidate => candidate.ModelYear == request.ModelYear && candidate.NhtsaModel == "EXPLORER",
            TestContext.Current.CancellationToken);

        // Either the vehicle was never created, or it was created and left empty. Both are the
        // transaction holding; a vehicle with records would mean a half-finished load was committed.
        var stored = vehicle is null
            ? 0
            : await context.SourceDocuments.CountAsync(
                document => document.VehicleId == vehicle.Id, TestContext.Current.CancellationToken);
        Assert.Equal(0, stored);
    }

    [Fact]
    public async Task OneFailedLoadDoesNotStopTheNextOneFromRunning()
    {
        // A single bad registration must not take the runner down with it.
        _nhtsa.FailRecallsFor("EXPLORER");
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var failing = await client.PostAsJsonAsync("/api/vehicles", BuildRequest(), TestContext.Current.CancellationToken);
        var failed = await WaitForFinishAsync(client, await IdOfAsync(failing));
        Assert.Equal("failed", failed.State);

        var second = await client.PostAsJsonAsync("/api/vehicles", BuildRequest(), TestContext.Current.CancellationToken);
        var secondLoad = await WaitForFinishAsync(client, await IdOfAsync(second));

        Assert.True(secondLoad.IsFinished);
    }

    private static async Task<long> IdOfAsync(HttpResponseMessage response)
    {
        var load = await response.Content.ReadFromJsonAsync<LoadResponse>(TestContext.Current.CancellationToken);
        return load!.Id;
    }

    /// <summary>Polls the load until it stops changing, or fails the test rather than hanging forever.</summary>
    private static async Task<LoadResponse> WaitForFinishAsync(HttpClient client, long jobId)
    {
        var deadline = DateTimeOffset.UtcNow + Patience;
        LoadResponse? load = null;
        while (DateTimeOffset.UtcNow < deadline)
        {
            load = await client.GetFromJsonAsync<LoadResponse>($"/api/loads/{jobId}", TestContext.Current.CancellationToken);
            if (load!.IsFinished)
            {
                return load;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), TestContext.Current.CancellationToken);
        }

        Assert.Fail($"Load {jobId} was still '{load?.State}' after {Patience.TotalSeconds} seconds.");
        throw new InvalidOperationException("unreachable");
    }

    /// <summary>
    /// A vehicle nothing else in the run shares. Vehicle identity is make, model and model year, so
    /// two tests using the same year would load into one vehicle and the second would correctly
    /// report every record as unchanged — which reads as "the load did nothing".
    /// </summary>
    private static RegisterVehicleRequest BuildRequest() => new(
        "Ford", "EXPLORER", null, NextModelYear(), $"Runner fixture {Guid.NewGuid():N}");

    private static int NextModelYear() => FirstModelYear + Interlocked.Increment(ref _yearOffset);

    /// <summary>
    /// Hosts the API with its background runner live, pointed at the recorded NHTSA server. Nothing
    /// here reaches the real feeds, and embedding is off so a load needs no key and costs nothing.
    /// </summary>
    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(AppSettings.ConnectionConfigurationKey, postgres.ConnectionString);
            builder.UseSetting("RecallRadar:NhtsaApiBaseUrl", _nhtsa.BaseUrl);
            builder.UseSetting("RecallRadar:InvestigationsFlatFileUrl", _nhtsa.FlatFileUrl);
            builder.UseSetting("IngestRunner:PollInterval", "00:00:00.100");
            builder.UseSetting("IngestRunner:EmbedAfterLoad", "false");
        });
}
