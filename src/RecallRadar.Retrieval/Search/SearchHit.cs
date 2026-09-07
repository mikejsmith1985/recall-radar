// One search result: the record, enough of it to read, and why it ranked where it did.
using RecallRadar.Domain.Retrieval;
using RecallRadar.Retrieval.Persistence;

using RecallRadar.Domain.Vehicles;

namespace RecallRadar.Retrieval.Search;

/// <summary>
/// What the search endpoint returns per hit. The explanation is part of the contract (FR-007):
/// a reader can see whether a record was found by meaning, by keyword, or by both.
/// </summary>
public sealed record SearchHit(
    long DocumentId,
    long ChunkId,
    SourceKind Kind,
    string ExternalId,
    string Title,
    string Component,
    DateOnly? FiledOn,
    string Snippet,
    RankExplanation Explanation,
    VehicleFit Fit)
{
    /// <summary>How much of a chunk is shown before it is cut at a word boundary.</summary>
    public const int SnippetLength = 240;

    private const string Ellipsis = "…";

    /// <summary>
    /// Shortens chunk text for display, cutting at the last space so a word is never split.
    /// The full body is fetched separately when the reader opens a record.
    /// </summary>
    public static string BuildSnippet(string chunkText)
    {
        ArgumentNullException.ThrowIfNull(chunkText);
        var collapsed = string.Join(' ', chunkText.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        if (collapsed.Length <= SnippetLength)
        {
            return collapsed;
        }

        var cut = collapsed.LastIndexOf(' ', SnippetLength);
        return string.Concat(collapsed[..(cut > 0 ? cut : SnippetLength)], Ellipsis);
    }
}
