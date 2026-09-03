// Checks the counts the embed verb prints match the command-line contract.
using RecallRadar.Ingest.Commands;

namespace RecallRadar.Unit.Commands;

public sealed class EmbedReportTests
{
    [Fact]
    public void FormatLines_RendersTheContractLine()
    {
        var report = new EmbedReport { Embedded = 2251, Remaining = 0 };

        Assert.Equal(["chunks: embedded 2251, remaining 0"], report.FormatLines());
    }

    [Fact]
    public void FormatLines_ReportsWhatIsLeftWhenALimitStoppedItEarly()
    {
        var report = new EmbedReport { Embedded = 128, Remaining = 96 };

        Assert.Equal(["chunks: embedded 128, remaining 96"], report.FormatLines());
    }

    [Fact]
    public void IsComplete_IsTrueOnlyWhenNothingIsLeft()
    {
        Assert.True(new EmbedReport { Embedded = 5, Remaining = 0 }.IsComplete);
        Assert.False(new EmbedReport { Embedded = 5, Remaining = 1 }.IsComplete);
    }
}
