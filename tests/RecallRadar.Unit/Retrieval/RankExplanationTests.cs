// Checks the explanation record that tells a user why a result ranked where it did.
using RecallRadar.Domain.Retrieval;

namespace RecallRadar.Unit.Retrieval;

public sealed class RankExplanationTests
{
    [Fact]
    public void WasFoundByBoth_RequiresBothRanks()
    {
        Assert.True(new RankExplanation(1, 3, 0.5).WasFoundByBoth);
        Assert.False(new RankExplanation(1, null, 0.5).WasFoundByBoth);
        Assert.False(new RankExplanation(null, 3, 0.5).WasFoundByBoth);
    }

    [Fact]
    public void SingleMethod_PutsTheRankUnderTheRightMethod()
    {
        var dense = RankExplanation.SingleMethod(RetrievalMode.Dense, 2, 0.9);
        var sparse = RankExplanation.SingleMethod(RetrievalMode.Sparse, 7, 0.1);

        Assert.Equal(new RankExplanation(2, null, 0.9), dense);
        Assert.Equal(new RankExplanation(null, 7, 0.1), sparse);
    }

    [Fact]
    public void SingleMethod_RejectsHybridBecauseHybridRanksComeFromFusion()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RankExplanation.SingleMethod(RetrievalMode.Hybrid, 1, 1));
    }
}
