// Formats what the evaluation measured, as a table an operator can read at a glance.
using System.Globalization;
using RecallRadar.Retrieval.Evaluation;

namespace RecallRadar.Ingest.Commands;

/// <summary>
/// Turns an evaluation result into the lines the command prints. Kept separate from running the
/// evaluation so the formatting is unit-tested without a database.
/// </summary>
public static class EvalReport
{
    /// <summary>Shown in a numeric column when a mode did not run, so the column never reads as zero.</summary>
    public const string NotRunMarker = "—";

    private const int PoolColumnWidth = 11;
    private const int ModeColumnWidth = 8;
    private const int NumberColumnWidth = 10;

    /// <summary>Formats the run: a header, one row per mode in each pool, then any reason a mode was skipped.</summary>
    public static IReadOnlyList<string> FormatLines(EvaluationResult result, TimeSpan elapsed)
    {
        ArgumentNullException.ThrowIfNull(result);

        var lines = new List<string>
        {
            $"cases: {result.CaseCount}",
            string.Empty,
            Row("pool", "mode", "recall@5", "recall@10", "mrr", "scored"),
            Row(new string('-', PoolColumnWidth), new string('-', ModeColumnWidth), new string('-', NumberColumnWidth),
                new string('-', NumberColumnWidth), new string('-', NumberColumnWidth), new string('-', NumberColumnWidth)),
        };

        lines.AddRange(result.Modes.Select(FormatMode));

        var skipped = result.Modes.Where(mode => !mode.WasScored && mode.SkippedReason is not null).ToList();
        if (skipped.Count > 0)
        {
            lines.Add(string.Empty);
            lines.AddRange(skipped.Select(mode => $"{mode.Key} not run: {mode.SkippedReason}"));
        }

        lines.Add(string.Empty);
        lines.Add($"done in {elapsed:hh\\:mm\\:ss}");
        return lines;
    }

    private static string FormatMode(ModeResult mode)
    {
        var pool = mode.Scope.ToString().ToLowerInvariant();
        return mode.Metrics is { } metrics
            ? Row(pool, mode.Mode, Format(metrics.RecallAt5), Format(metrics.RecallAt10),
                Format(metrics.MeanReciprocalRank), metrics.ScoredCaseCount.ToString(CultureInfo.InvariantCulture))
            : Row(pool, mode.Mode, NotRunMarker, NotRunMarker, NotRunMarker, NotRunMarker);
    }

    /// <summary>Three decimals: enough to separate two modes, few enough not to imply precision that is not there.</summary>
    private static string Format(double value) => value.ToString("0.000", CultureInfo.InvariantCulture);

    private static string Row(
        string pool, string mode, string recallAt5, string recallAt10, string meanReciprocalRank, string scored) =>
        string.Concat(
            pool.PadRight(PoolColumnWidth),
            mode.PadRight(ModeColumnWidth),
            recallAt5.PadLeft(NumberColumnWidth),
            recallAt10.PadLeft(NumberColumnWidth),
            meanReciprocalRank.PadLeft(NumberColumnWidth),
            scored.PadLeft(NumberColumnWidth));
}
