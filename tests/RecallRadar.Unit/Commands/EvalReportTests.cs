// Checks the table an operator reads, especially that a skipped mode never looks like a bad one.
using RecallRadar.Domain.Evaluation;
using RecallRadar.Ingest.Commands;
using RecallRadar.Retrieval.Evaluation;

namespace RecallRadar.Unit.Commands;

public sealed class EvalReportTests
{
    private static readonly DateTimeOffset RanAt = new(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan Elapsed = TimeSpan.FromSeconds(42);

    [Fact]
    public void FormatLines_ShowsEveryModeWithItsNumbers()
    {
        var result = new EvaluationResult(RanAt, 41,
        [
            Scored("sparse", 0.62, 0.71, 0.55, 41),
            Scored("dense", 0.48, 0.60, 0.41, 41),
            Scored("hybrid", 0.70, 0.80, 0.63, 41),
        ]);

        var lines = EvalReport.FormatLines(result, Elapsed);
        var text = string.Join('\n', lines);

        Assert.Contains("cases: 41", text, StringComparison.Ordinal);
        Assert.Contains("0.620", text, StringComparison.Ordinal);
        Assert.Contains("0.800", text, StringComparison.Ordinal);
        Assert.Contains("hybrid", text, StringComparison.Ordinal);
        Assert.Contains("done in 00:00:42", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatLines_MarksASkippedModeRatherThanPrintingZero()
    {
        // A zero would read as "hybrid is bad" when it means "hybrid was not tried".
        var result = new EvaluationResult(RanAt, 41,
        [
            Scored("sparse", 0.62, 0.71, 0.55, 41),
            new ModeResult("dense", null, "No chunk for this vehicle has an embedding yet."),
        ]);

        var text = string.Join('\n', EvalReport.FormatLines(result, Elapsed));

        Assert.Contains(EvalReport.NotRunMarker, text, StringComparison.Ordinal);
        Assert.DoesNotContain("dense       0.000", text, StringComparison.Ordinal);
        Assert.Contains("dense not run: No chunk for this vehicle has an embedding yet.", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatLines_SaysNothingAboutSkippingWhenEveryModeRan()
    {
        var result = new EvaluationResult(RanAt, 5, [Scored("sparse", 0.5, 0.5, 0.5, 5)]);

        var text = string.Join('\n', EvalReport.FormatLines(result, Elapsed));

        Assert.DoesNotContain("not run", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatLines_HandlesARunWithNoCasesWithoutBreaking()
    {
        var result = new EvaluationResult(RanAt, 0,
            [new ModeResult("sparse", null, "No ground-truth cases exist yet. Load records first.")]);

        var text = string.Join('\n', EvalReport.FormatLines(result, Elapsed));

        Assert.Contains("cases: 0", text, StringComparison.Ordinal);
        Assert.Contains("Load records first", text, StringComparison.Ordinal);
    }

    [Fact]
    public void FormatLines_UsesThreeDecimalsSoTwoModesCanBeToldApart()
    {
        var result = new EvaluationResult(RanAt, 2, [Scored("sparse", 0.6666, 0.5, 0.3333, 2)]);

        var text = string.Join('\n', EvalReport.FormatLines(result, Elapsed));

        Assert.Contains("0.667", text, StringComparison.Ordinal);
        Assert.Contains("0.333", text, StringComparison.Ordinal);
    }

    private static ModeResult Scored(string mode, double recallAt5, double recallAt10, double mrr, int scored) =>
        new(mode, new RetrievalMetricsSummary(recallAt5, recallAt10, mrr, scored, 0), null);
}
