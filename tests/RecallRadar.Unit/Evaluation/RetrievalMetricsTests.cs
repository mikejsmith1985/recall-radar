// Hand-computed recall@k and MRR cases, including the empty-relevant-set rule.
using RecallRadar.Domain.Evaluation;

namespace RecallRadar.Unit.Evaluation;

public sealed class RetrievalMetricsTests
{
    private const double Tolerance = 1e-12;

    [Fact]
    public void RecallAtK_CountsRelevantIdsInsideTheCutoffOnly()
    {
        long[] retrieved = [1, 2, 3, 4, 5, 6];
        var relevant = new HashSet<long> { 2, 6, 99 };

        Assert.Equal(1.0 / 3, RetrievalMetrics.RecallAtK(retrieved, relevant, 5), Tolerance);
        Assert.Equal(2.0 / 3, RetrievalMetrics.RecallAtK(retrieved, relevant, 10), Tolerance);
    }

    [Fact]
    public void RecallAtK_DuplicateRetrievedIdsCountOnce()
    {
        long[] retrieved = [2, 2, 2];

        Assert.Equal(0.5, RetrievalMetrics.RecallAtK(retrieved, new HashSet<long> { 2, 3 }, 5), Tolerance);
    }

    [Fact]
    public void RecallAtK_EmptyRelevantSetIsZeroNotAnError()
    {
        Assert.Equal(0, RetrievalMetrics.RecallAtK([1, 2], new HashSet<long>(), 5));
    }

    [Fact]
    public void ReciprocalRank_UsesTheFirstRelevantPosition()
    {
        var relevant = new HashSet<long> { 30, 40 };

        Assert.Equal(1.0 / 3, RetrievalMetrics.ReciprocalRank([10, 20, 30, 40], relevant), Tolerance);
        Assert.Equal(0, RetrievalMetrics.ReciprocalRank([10, 20], relevant));
    }

    [Fact]
    public void Compute_AveragesOverScoredCasesAndSkipsCasesWithNoRelevantDocuments()
    {
        var cases = new List<GroundTruthCase>
        {
            GroundTruthCase.Create("a", "q", [1L]),
            GroundTruthCase.Create("b", "q", [2L, 3L]),
            GroundTruthCase.Create("skip", "q", []),
        };
        var rankings = new Dictionary<string, IReadOnlyList<long>>
        {
            ["a"] = [9, 1],
            ["b"] = [2, 8, 8, 8, 8, 8, 8, 8, 8, 3],
            ["skip"] = [1, 2, 3],
        };

        var summary = RetrievalMetrics.Compute(cases, rankings);

        // a: recall@5 = 1, recall@10 = 1, RR = 1/2. b: recall@5 = 1/2, recall@10 = 1, RR = 1.
        Assert.Equal(0.75, summary.RecallAt5, Tolerance);
        Assert.Equal(1.0, summary.RecallAt10, Tolerance);
        Assert.Equal(0.75, summary.MeanReciprocalRank, Tolerance);
        Assert.Equal(2, summary.ScoredCaseCount);
        Assert.Equal(1, summary.SkippedCaseCount);
    }

    [Fact]
    public void Compute_CaseWithNoRankingCountsAsRetrievingNothing()
    {
        var cases = new List<GroundTruthCase> { GroundTruthCase.Create("a", "q", [1L]) };

        var summary = RetrievalMetrics.Compute(cases, new Dictionary<string, IReadOnlyList<long>>());

        Assert.Equal(0, summary.RecallAt10);
        Assert.Equal(0, summary.MeanReciprocalRank);
        Assert.Equal(1, summary.ScoredCaseCount);
    }

    [Fact]
    public void Compute_NoScorableCasesReportsZeroWithoutDividing()
    {
        var summary = RetrievalMetrics.Compute([GroundTruthCase.Create("a", "q", [])], new Dictionary<string, IReadOnlyList<long>>());

        Assert.Equal(0, summary.ScoredCaseCount);
        Assert.Equal(1, summary.SkippedCaseCount);
        Assert.Equal(0, summary.MeanReciprocalRank);
    }

    [Fact]
    public void Compute_IsDeterministicAcrossRuns()
    {
        var cases = new List<GroundTruthCase> { GroundTruthCase.Create("a", "q", [1L, 2L]) };
        var rankings = new Dictionary<string, IReadOnlyList<long>> { ["a"] = [2, 5, 1] };

        Assert.Equal(RetrievalMetrics.Compute(cases, rankings), RetrievalMetrics.Compute(cases, rankings));
    }
}
