// One retrieved chunk with the record it belongs to and the explanation of its rank.
namespace RecallRadar.Domain.Retrieval;

/// <summary>
/// The unit the search endpoint returns. Chunk and document ids are both carried because the
/// evaluation counts relevance per document while retrieval ranks per chunk.
/// </summary>
/// <param name="DocumentId">The source record the chunk was cut from.</param>
/// <param name="ChunkId">The chunk that was actually matched.</param>
/// <param name="Explanation">Ranks under each method and the fused score.</param>
public sealed record RankedHit(long DocumentId, long ChunkId, RankExplanation Explanation)
{
    /// <summary>
    /// Collapses chunk-level hits to document-level order, keeping each document's best-ranked
    /// chunk. Order is preserved, so the first appearance of a document is its rank.
    /// </summary>
    public static IReadOnlyList<long> DistinctDocumentOrder(IEnumerable<RankedHit> hits)
    {
        ArgumentNullException.ThrowIfNull(hits);
        var seen = new HashSet<long>();
        var order = new List<long>();
        foreach (var hit in hits)
        {
            if (seen.Add(hit.DocumentId))
            {
                order.Add(hit.DocumentId);
            }
        }

        return order;
    }
}
