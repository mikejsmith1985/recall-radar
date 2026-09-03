// Scores a retrieval mode against ground truth: recall@k and mean reciprocal rank.
namespace RecallRadar.Domain.Evaluation;

/// <summary>The headline numbers for one retrieval mode over one ground-truth set.</summary>
/// <param name="RecallAt5">Average share of relevant documents found in the top five.</param>
/// <param name="RecallAt10">Average share of relevant documents found in the top ten.</param>
/// <param name="MeanReciprocalRank">Average of 1 / (rank of the first relevant document).</param>
/// <param name="ScoredCaseCount">Cases that contributed to the averages.</param>
/// <param name="SkippedCaseCount">Cases with no relevant documents, excluded so they cannot drag averages to zero.</param>
public sealed record RetrievalMetricsSummary(
    double RecallAt5,
    double RecallAt10,
    double MeanReciprocalRank,
    int ScoredCaseCount,
    int SkippedCaseCount);

/// <summary>
/// Pure metric arithmetic. Everything is deterministic so re-running the evaluation on unchanged
/// data reproduces every number exactly (SC-006).
/// </summary>
public static class RetrievalMetrics
{
    public const int RecallCutoffFive = 5;
    public const int RecallCutoffTen = 10;

    /// <summary>
    /// Share of the relevant set that appears in the first k retrieved ids. Returns 0 for an empty
    /// relevant set; callers that average must skip such cases, which <see cref="Compute"/> does.
    /// </summary>
    public static double RecallAtK(IReadOnlyList<long> retrievedIds, IReadOnlySet<long> relevantIds, int k)
    {
        ArgumentNullException.ThrowIfNull(retrievedIds);
        ArgumentNullException.ThrowIfNull(relevantIds);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(k);
        if (relevantIds.Count == 0)
        {
            return 0;
        }

        var found = retrievedIds.Take(k).Distinct().Count(relevantIds.Contains);
        return (double)found / relevantIds.Count;
    }

    /// <summary>1 / (one-based rank of the first relevant id), or 0 when none was retrieved.</summary>
    public static double ReciprocalRank(IReadOnlyList<long> retrievedIds, IReadOnlySet<long> relevantIds)
    {
        ArgumentNullException.ThrowIfNull(retrievedIds);
        ArgumentNullException.ThrowIfNull(relevantIds);

        for (var position = 0; position < retrievedIds.Count; position++)
        {
            if (relevantIds.Contains(retrievedIds[position]))
            {
                return 1.0 / (position + 1);
            }
        }

        return 0;
    }

    /// <summary>
    /// Averages the metrics over every case that has relevant documents. A case with no ranking
    /// recorded counts as retrieving nothing, because that is what the user would have seen.
    /// </summary>
    public static RetrievalMetricsSummary Compute(
        IReadOnlyList<GroundTruthCase> cases, IReadOnlyDictionary<string, IReadOnlyList<long>> rankingsByCaseId)
    {
        ArgumentNullException.ThrowIfNull(cases);
        ArgumentNullException.ThrowIfNull(rankingsByCaseId);

        var scored = cases.Where(groundTruth => groundTruth.HasRelevantDocuments).ToList();
        var skippedCount = cases.Count - scored.Count;
        if (scored.Count == 0)
        {
            return new RetrievalMetricsSummary(0, 0, 0, 0, skippedCount);
        }

        double recallAt5 = 0, recallAt10 = 0, reciprocalRankTotal = 0;
        foreach (var groundTruth in scored)
        {
            var retrieved = rankingsByCaseId.GetValueOrDefault(groundTruth.CaseId) ?? [];
            recallAt5 += RecallAtK(retrieved, groundTruth.RelevantDocumentIds, RecallCutoffFive);
            recallAt10 += RecallAtK(retrieved, groundTruth.RelevantDocumentIds, RecallCutoffTen);
            reciprocalRankTotal += ReciprocalRank(retrieved, groundTruth.RelevantDocumentIds);
        }

        return new RetrievalMetricsSummary(
            recallAt5 / scored.Count,
            recallAt10 / scored.Count,
            reciprocalRankTotal / scored.Count,
            scored.Count,
            skippedCount);
    }
}
