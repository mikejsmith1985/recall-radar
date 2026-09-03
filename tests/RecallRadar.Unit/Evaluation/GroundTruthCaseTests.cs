// Checks construction rules for a ground-truth case.
using RecallRadar.Domain.Evaluation;

namespace RecallRadar.Unit.Evaluation;

public sealed class GroundTruthCaseTests
{
    [Fact]
    public void Create_TrimsIdDeduplicatesRelevantIdsAndReportsPresence()
    {
        var groundTruth = GroundTruthCase.Create(" 11760888 ", "Exhaust odor in cabin", [5L, 5L, 9L]);

        Assert.Equal("11760888", groundTruth.CaseId);
        Assert.Equal(2, groundTruth.RelevantDocumentIds.Count);
        Assert.True(groundTruth.HasRelevantDocuments);
    }

    [Fact]
    public void Create_WithNoRelevantDocumentsIsAllowedButFlagged()
    {
        var groundTruth = GroundTruthCase.Create("1", "query", []);

        Assert.False(groundTruth.HasRelevantDocuments);
    }

    [Theory]
    [InlineData("", "query")]
    [InlineData("1", " ")]
    public void Create_RejectsBlankIdOrQuery(string caseId, string query)
    {
        Assert.Throws<ArgumentException>(() => GroundTruthCase.Create(caseId, query, [1L]));
    }
}
