// Checks the load report renders the CLI contract lines.
using RecallRadar.Ingest;
using RecallRadar.Ingest.Config;

namespace RecallRadar.Unit;

public sealed class IngestReportTests
{
    private static readonly VehicleRegistration Explorer = new() { Make = "FORD", NhtsaModel = "EXPLORER", ModelYear = 2013, DisplayName = "2013 Explorer Sport" };

    [Fact]
    public void FormatLines_FollowsTheContractAndExplainsMissingEmbeddings()
    {
        var report = new IngestReport
        {
            Vehicle = Explorer,
            ComplaintsFetched = 2231, ComplaintsNew = 2231, ComplaintsUnchanged = 0, ComplaintsSkippedEmpty = 0,
            RecallsFetched = 12, RecallsNew = 12, RecallsUnchanged = 0,
            InvestigationRows = 8, InvestigationsNew = 6, LinksCreated = 2,
            ChunksCreated = 2251, ChunksEmbedded = 0,
        };

        var lines = report.FormatLines(TimeSpan.FromSeconds(102));

        Assert.Equal("vehicle: 2013 Explorer Sport (FORD 2013, models: EXPLORER)", lines[0]);
        Assert.Equal("complaints: fetched 2231, new 2231, unchanged 0, skipped empty 0", lines[1]);
        Assert.Equal("recalls: fetched 12, new 12, unchanged 0", lines[2]);
        Assert.Equal("investigations: rows 8, new 6, links 2", lines[3]);
        Assert.Equal($"chunks: created 2251, embedded 0 ({IngestReport.EmbeddingsUnavailableNote})", lines[4]);
        Assert.Equal("done in 00:01:42", lines[5]);
    }

    [Fact]
    public void FormatLines_OmitsTheEmbeddingNoteWhenNothingWasCreated()
    {
        var report = new IngestReport { Vehicle = Explorer };

        Assert.Equal("chunks: created 0, embedded 0", report.FormatLines(TimeSpan.Zero)[4]);
    }
}
