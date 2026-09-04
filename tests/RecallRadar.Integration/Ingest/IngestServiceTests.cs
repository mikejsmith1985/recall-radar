// Runs the real ingest verb end to end against the recorded NHTSA server and the container database.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using RecallRadar.Ingest;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Integration.Ingest;

[Collection(PostgresCollection.Name)]
public sealed class IngestServiceTests(PostgresFixture postgres) : IDisposable
{
    private const string ExplorerName = "2012 Explorer (fixture)";
    private const string FailingName = "2013 Taurus (failing fixture)";
    private const string FailingModel = "TAURUS";

    private readonly NhtsaFixtureServer _nhtsa = new();

    [Fact]
    public async Task Ingest_StoresDeduplicatedRecordsChunksAndLinks_AndIsIdempotent()
    {
        var first = await RunIngestAsync(ExplorerName);
        var second = await RunIngestAsync(ExplorerName);

        Assert.Equal(0, first.ExitCode);
        Assert.Contains("complaints: fetched 6, new 5, unchanged 0, skipped empty 0", first.Stdout);
        Assert.Contains("recalls: fetched 3, new 3, unchanged 0", first.Stdout);
        Assert.Contains("investigations: rows 3, new 2, links 1", first.Stdout);
        Assert.Contains("chunks: created 10, embedded 0", first.Stdout);
        Assert.Equal(0, second.ExitCode);
        Assert.Contains("complaints: fetched 6, new 0, unchanged 5", second.Stdout);
        Assert.Contains("recalls: fetched 3, new 0, unchanged 3", second.Stdout);
        Assert.Contains("investigations: rows 3, new 0, links 0", second.Stdout);
        Assert.Contains("chunks: created 0", second.Stdout);

        await using var context = postgres.CreateContext();
        var vehicle = await context.Vehicles.SingleAsync(entity => entity.NhtsaModel == "EXPLORER" && entity.ModelYear == NhtsaFixtureServer.FixtureModelYear, TestContext.Current.CancellationToken);
        var documents = await context.SourceDocuments.Where(entity => entity.VehicleId == vehicle.Id).ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(5, documents.Count(entity => entity.Kind == SourceKind.Complaint));
        Assert.Equal(3, documents.Count(entity => entity.Kind == SourceKind.Recall));
        Assert.Equal(2, documents.Count(entity => entity.Kind == SourceKind.Investigation));
        var exhaust = documents.Single(entity => entity.ExternalId == "EA17002");
        Assert.Equal("ENGINE AND ENGINE COOLING:EXHAUST SYSTEM; STRUCTURE:BODY", exhaust.Component);
        // Scoped to this test's own vehicle: the container is shared, and other tests create links
        // of their own. An unscoped Single here passed alone and failed in company.
        var link = await context.InvestigationLinks
            .SingleAsync(entity => entity.InvestigationDocument!.VehicleId == vehicle.Id, TestContext.Current.CancellationToken);
        Assert.Equal("19V435000", link.CampaignNumber);
        Assert.Equal(10, await context.DocumentChunks.CountAsync(
            entity => entity.Document!.VehicleId == vehicle.Id, TestContext.Current.CancellationToken));
        Assert.All(_nhtsa.ReceivedMethods, method => Assert.Equal("GET", method));
    }

    [Fact]
    public async Task Ingest_WhenAFeedFailsMidLoad_StoresNothingAndReportsAnError()
    {
        _nhtsa.FailRecallsFor(FailingModel);

        var result = await RunIngestAsync(FailingName);

        Assert.Equal(1, result.ExitCode);
        Assert.StartsWith(IngestHost.ErrorPrefix, result.Stderr.Trim());
        await using var context = postgres.CreateContext();
        Assert.False(await context.Vehicles.AnyAsync(entity => entity.NhtsaModel == FailingModel, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Ingest_UnknownVehicleName_FailsBeforeTouchingTheNetwork()
    {
        var result = await RunIngestAsync("1999 Pinto");

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("No registered vehicle is named '1999 Pinto'", result.Stderr);
        Assert.Empty(_nhtsa.ReceivedMethods);
    }

    public void Dispose() => _nhtsa.Dispose();

    private async Task<(int ExitCode, string Stdout, string Stderr)> RunIngestAsync(string vehicleName)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var exitCode = await IngestCommandLine.RunAsync(
            ["ingest", "--vehicle", vehicleName], stdout, stderr,
            builder => builder.Configuration.AddInMemoryCollection(BuildConfiguration()));
        return (exitCode, stdout.ToString(), stderr.ToString());
    }

    private Dictionary<string, string?> BuildConfiguration() => new()
    {
        [IngestHost.ConnectionConfigurationKey] = postgres.ConnectionString,
        ["RecallRadar:NhtsaApiBaseUrl"] = _nhtsa.BaseUrl,
        ["RecallRadar:InvestigationsFlatFileUrl"] = _nhtsa.FlatFileUrl,
        ["RecallRadar:Vehicles:0:Make"] = "FORD",
        ["RecallRadar:Vehicles:0:NhtsaModel"] = "EXPLORER",
        ["RecallRadar:Vehicles:0:ModelYear"] = NhtsaFixtureServer.FixtureModelYear.ToString(),
        ["RecallRadar:Vehicles:0:DisplayName"] = ExplorerName,
        ["RecallRadar:Vehicles:1:Make"] = "FORD",
        ["RecallRadar:Vehicles:1:NhtsaModel"] = FailingModel,
        ["RecallRadar:Vehicles:1:ModelYear"] = "2013",
        ["RecallRadar:Vehicles:1:DisplayName"] = FailingName,
    };
}
