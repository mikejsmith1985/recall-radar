// Checks the constants and failure wording the live model path depends on.
using RecallRadar.Api.Answering;

namespace RecallRadar.Unit.Answering;

/// <summary>
/// The request itself is exercised against the real API in the live check recorded in the
/// changelog; what is worth pinning here is everything a reader or a caller depends on being
/// stable, and the promise that a model failure never escapes as an unhandled error.
/// </summary>
public sealed class ClaudeAnswerModelTests
{
    [Fact]
    public void ModelId_IsTheModelThisApplicationWasWrittenAgainst()
    {
        Assert.Equal("claude-opus-5", ClaudeAnswerModel.ModelId);
    }

    [Fact]
    public void MaxTokens_LeavesRoomForAnAnswerWithoutInvitingAnEssay()
    {
        Assert.InRange(ClaudeAnswerModel.MaxTokens, 2048, 16000);
    }

    [Fact]
    public void EveryDeclineReasonReadsAsSomethingAReaderCanActOn()
    {
        foreach (var reason in new[]
        {
            ClaudeAnswerModel.EmptyReplyReason,
            ClaudeAnswerModel.RefusalReason,
            ClaudeAnswerModel.OutageReason,
        })
        {
            Assert.False(string.IsNullOrWhiteSpace(reason));
            Assert.EndsWith(".", reason.Trim(), StringComparison.Ordinal);
        }
    }

    [Fact]
    public void TheOutageReasonSuggestsRetryingRatherThanReportingAFault()
    {
        // An outage is temporary and not the reader's problem to diagnose.
        Assert.Contains("again", ClaudeAnswerModel.OutageReason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ARefusalAndAnOutageAreDistinguishable()
    {
        // They mean different things: one is the model declining, the other is it being unreachable.
        Assert.NotEqual(ClaudeAnswerModel.RefusalReason, ClaudeAnswerModel.OutageReason);
    }
}
