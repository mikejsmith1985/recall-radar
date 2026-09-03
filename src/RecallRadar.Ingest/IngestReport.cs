// The counts a load produces, and the lines the CLI prints for them.
using RecallRadar.Ingest.Config;

namespace RecallRadar.Ingest;

/// <summary>
/// What happened during one vehicle load. "Unchanged" means the record was already stored from an
/// earlier run, which is how a repeat load proves it created no duplicates (FR-003).
/// </summary>
public sealed class IngestReport
{
    public const string EmbeddingsUnavailableNote = "embeddings unavailable: VOYAGE_API_KEY not set";

    public required VehicleRegistration Vehicle { get; init; }
    public int ComplaintsFetched { get; init; }
    public int ComplaintsSkippedEmpty { get; init; }
    public int ComplaintsNew { get; init; }
    public int RecallsFetched { get; init; }
    public int RecallsNew { get; init; }
    public int InvestigationRows { get; init; }
    public int InvestigationsNew { get; init; }
    public int LinksCreated { get; init; }
    public int ChunksCreated { get; init; }
    public int ChunksEmbedded { get; init; }

    /// <summary>Fetched records that were already stored. Distinct records only, so duplicates in the feed do not count.</summary>
    public int ComplaintsUnchanged { get; init; }
    public int RecallsUnchanged { get; init; }

    /// <summary>Renders the contract output from <c>specs/001-recall-radar/contracts/cli.md</c>.</summary>
    public IReadOnlyList<string> FormatLines(TimeSpan elapsed)
    {
        var embeddingNote = ChunksEmbedded == 0 && ChunksCreated > 0 ? $" ({EmbeddingsUnavailableNote})" : string.Empty;
        return
        [
            $"vehicle: {Vehicle.DisplayName} ({Vehicle.Make} {Vehicle.ModelYear}, models: {Vehicle.NhtsaModel})",
            $"complaints: fetched {ComplaintsFetched}, new {ComplaintsNew}, unchanged {ComplaintsUnchanged}, skipped empty {ComplaintsSkippedEmpty}",
            $"recalls: fetched {RecallsFetched}, new {RecallsNew}, unchanged {RecallsUnchanged}",
            $"investigations: rows {InvestigationRows}, new {InvestigationsNew}, links {LinksCreated}",
            $"chunks: created {ChunksCreated}, embedded {ChunksEmbedded}{embeddingNote}",
            $"done in {elapsed:hh\\:mm\\:ss}",
        ];
    }
}
