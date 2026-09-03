// Checks the collapse from chunk-level hits to document-level order used by the evaluation.
using RecallRadar.Domain.Retrieval;

namespace RecallRadar.Unit.Retrieval;

public sealed class RankedHitTests
{
    private static RankedHit Hit(long documentId, long chunkId) =>
        new(documentId, chunkId, new RankExplanation(null, null, 0));

    [Fact]
    public void DistinctDocumentOrder_KeepsFirstAppearanceOfEachDocument()
    {
        var hits = new[] { Hit(10, 1), Hit(20, 2), Hit(10, 3), Hit(30, 4), Hit(20, 5) };

        var order = RankedHit.DistinctDocumentOrder(hits);

        Assert.Equal([10L, 20L, 30L], order);
    }

    [Fact]
    public void DistinctDocumentOrder_EmptyInputGivesEmptyOrder()
    {
        Assert.Empty(RankedHit.DistinctDocumentOrder([]));
    }
}
