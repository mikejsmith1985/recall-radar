// Hand-computed reciprocal rank fusion cases so the arithmetic is checked, not assumed.
using RecallRadar.Domain.Retrieval;

namespace RecallRadar.Unit.Retrieval;

public sealed class ReciprocalRankFusionTests
{
    private const double Tolerance = 1e-12;

    [Fact]
    public void Fuse_ItemFirstInBothListsScoresTwoOverKPlusOne()
    {
        var fused = ReciprocalRankFusion.Fuse([[7L, 8L], [7L, 9L]]);

        Assert.Equal(7L, fused[0].Id);
        Assert.Equal(2.0 / 61, fused[0].Score, Tolerance);
        Assert.Equal([1, 1], fused[0].RanksPerList);
    }

    [Fact]
    public void Fuse_ItemInOneListOnlyScoresOneOverKPlusRankAndReportsNullForTheOther()
    {
        var fused = ReciprocalRankFusion.Fuse([[7L, 8L], [7L, 9L]]);

        var eight = Assert.Single(fused, item => item.Id == 8);
        Assert.Equal(1.0 / 62, eight.Score, Tolerance);
        Assert.Equal([2, null], eight.RanksPerList);
    }

    [Fact]
    public void Fuse_ItemFoundByBothAtModestRanksBeatsItemFoundOnceAtRankOne()
    {
        // 5 is rank 1 in dense only: 1/61 ≈ 0.01639. 6 is rank 2 in both: 2/62 ≈ 0.03226.
        var fused = ReciprocalRankFusion.Fuse([[5L, 6L], [9L, 6L]]);

        Assert.Equal(6L, fused[0].Id);
        Assert.Equal(2.0 / 62, fused[0].Score, Tolerance);
    }

    [Fact]
    public void Fuse_BreaksTiesByLowerIdSoOutputIsDeterministic()
    {
        var fused = ReciprocalRankFusion.Fuse([[42L], [17L]]);

        Assert.Equal([17L, 42L], fused.Select(item => item.Id));
        Assert.Equal(fused[0].Score, fused[1].Score, Tolerance);
    }

    [Fact]
    public void Fuse_EmptyListsGiveEmptyResult()
    {
        Assert.Empty(ReciprocalRankFusion.Fuse([[], []]));
        Assert.Empty(ReciprocalRankFusion.Fuse([]));
    }

    [Fact]
    public void Fuse_RepeatedIdWithinOneListKeepsItsBestRank()
    {
        var fused = ReciprocalRankFusion.Fuse([[3L, 3L]]);

        var only = Assert.Single(fused);
        Assert.Equal([1], only.RanksPerList);
        Assert.Equal(1.0 / 61, only.Score, Tolerance);
    }

    [Fact]
    public void Fuse_RejectsNonPositiveK()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ReciprocalRankFusion.Fuse([[1L]], k: 0));
    }

    [Fact]
    public void FuseDenseAndSparse_ProducesExplanationsWithDenseFirstAndSparseSecond()
    {
        var fused = ReciprocalRankFusion.FuseDenseAndSparse([7L, 8L], [9L, 7L]);

        Assert.Equal(7L, fused[0].ChunkId);
        Assert.Equal(new RankExplanation(1, 2, 1.0 / 61 + 1.0 / 62), fused[0].Explanation);
        var nine = Assert.Single(fused, item => item.ChunkId == 9);
        Assert.Equal(new RankExplanation(null, 1, 1.0 / 61), nine.Explanation);
    }
}
