// Downloads the synthetic flat file from the recorded server and parses it through the real client.
using RecallRadar.Ingest.Config;
using RecallRadar.Ingest.Nhtsa;

namespace RecallRadar.Integration.Ingest;

public sealed class NhtsaFlatFileClientTests : IDisposable
{
    private readonly NhtsaFixtureServer _nhtsa = new();

    [Fact]
    public async Task DownloadAsync_ReturnsAnArchiveTheParserCanRead()
    {
        using var httpClient = new HttpClient();
        var client = new NhtsaFlatFileClient(httpClient);
        var explorer = new VehicleRegistration { Make = "FORD", NhtsaModel = "EXPLORER", ModelYear = NhtsaFixtureServer.FixtureModelYear, DisplayName = "2012 Explorer (fixture)" };

        var archive = await client.DownloadAsync(new Uri(_nhtsa.FlatFileUrl), TestContext.Current.CancellationToken);
        var rows = InvestigationFlatFileParser.ParseZip(archive, [explorer]);

        Assert.Equal(3, rows.Count);
        Assert.Equal(["EA17002", "EA17002", "PE16008"], rows.Select(row => row.ActionNumber));
    }

    [Fact]
    public async Task DownloadAsync_RejectsRelativeAddresses()
    {
        using var httpClient = new HttpClient();
        var client = new NhtsaFlatFileClient(httpClient);

        await Assert.ThrowsAsync<ArgumentException>(() => client.DownloadAsync(new Uri("inv/FLAT_INV.zip", UriKind.Relative), TestContext.Current.CancellationToken));
    }

    public void Dispose() => _nhtsa.Dispose();
}
