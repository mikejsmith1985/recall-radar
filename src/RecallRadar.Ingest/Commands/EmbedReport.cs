// The counts a back-fill produces, and the line the CLI prints for them.
namespace RecallRadar.Ingest.Commands;

/// <summary>
/// What the embed verb did. <see cref="Remaining"/> is what still has no vector after the run, so a
/// non-zero value says plainly that another pass is needed rather than leaving it to be inferred.
/// </summary>
public sealed class EmbedReport
{
    /// <summary>Chunks given a vector during this run.</summary>
    public int Embedded { get; init; }

    /// <summary>Chunks that still have none.</summary>
    public int Remaining { get; init; }

    /// <summary>Whether every chunk now carries a vector.</summary>
    public bool IsComplete => Remaining == 0;

    /// <summary>Renders the contract output from <c>specs/001-recall-radar/contracts/cli.md</c>.</summary>
    public IReadOnlyList<string> FormatLines() => [$"chunks: embedded {Embedded}, remaining {Remaining}"];
}
