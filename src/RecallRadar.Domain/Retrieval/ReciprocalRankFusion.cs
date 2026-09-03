// Combines several ranked lists into one by rewarding items that rank well in more than one list.
namespace RecallRadar.Domain.Retrieval;

/// <summary>One item's fused result: its score and its one-based rank in each input list (null where absent).</summary>
public sealed record FusedRank(long Id, double Score, IReadOnlyList<int?> RanksPerList);

/// <summary>
/// Reciprocal Rank Fusion: score = Σ over lists of 1 / (k + rank). It needs no score calibration
/// between methods, only their orderings, which is why it is the standard way to merge a
/// keyword ranking with an embedding ranking.
/// </summary>
public static class ReciprocalRankFusion
{
    /// <summary>
    /// The constant from the original RRF paper (Cormack, Clarke and Buettcher, 2009). It damps the
    /// advantage of the very top ranks so that an item found by both methods at modest ranks can beat
    /// an item found by one method at rank one. Sixty is the value the paper found robust; tuning it
    /// per corpus buys little and costs reproducibility.
    /// </summary>
    public const int DefaultK = 60;

    /// <summary>
    /// Fuses any number of ranked id lists. Output is ordered by score descending, then by id
    /// ascending so equal scores always come out in the same order.
    /// </summary>
    public static IReadOnlyList<FusedRank> Fuse(IReadOnlyList<IReadOnlyList<long>> rankedLists, int k = DefaultK)
    {
        ArgumentNullException.ThrowIfNull(rankedLists);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(k);

        var ranksById = CollectRanks(rankedLists);
        return ranksById
            .Select(entry => new FusedRank(entry.Key, ScoreOf(entry.Value, k), entry.Value))
            .OrderByDescending(fused => fused.Score)
            .ThenBy(fused => fused.Id)
            .ToList();
    }

    /// <summary>
    /// The two-list case the search service uses. Returns hits keyed by chunk id with a full
    /// <see cref="RankExplanation"/>; the caller attaches document ids.
    /// </summary>
    public static IReadOnlyList<(long ChunkId, RankExplanation Explanation)> FuseDenseAndSparse(
        IReadOnlyList<long> denseOrder, IReadOnlyList<long> sparseOrder, int k = DefaultK)
    {
        ArgumentNullException.ThrowIfNull(denseOrder);
        ArgumentNullException.ThrowIfNull(sparseOrder);

        return Fuse([denseOrder, sparseOrder], k)
            .Select(fused => (fused.Id, new RankExplanation(fused.RanksPerList[0], fused.RanksPerList[1], fused.Score)))
            .ToList();
    }

    /// <summary>Records each id's one-based rank in each list; an id missing from a list keeps null there.</summary>
    private static Dictionary<long, int?[]> CollectRanks(IReadOnlyList<IReadOnlyList<long>> rankedLists)
    {
        var ranksById = new Dictionary<long, int?[]>();
        for (var listIndex = 0; listIndex < rankedLists.Count; listIndex++)
        {
            var list = rankedLists[listIndex] ?? throw new ArgumentException("A ranked list was null.", nameof(rankedLists));
            for (var position = 0; position < list.Count; position++)
            {
                if (!ranksById.TryGetValue(list[position], out var ranks))
                {
                    ranks = new int?[rankedLists.Count];
                    ranksById[list[position]] = ranks;
                }

                // Keep the first (best) rank if an id is repeated within one list.
                ranks[listIndex] ??= position + 1;
            }
        }

        return ranksById;
    }

    private static double ScoreOf(IReadOnlyList<int?> ranks, int k) =>
        ranks.Where(rank => rank.HasValue).Sum(rank => 1.0 / (k + rank!.Value));
}
