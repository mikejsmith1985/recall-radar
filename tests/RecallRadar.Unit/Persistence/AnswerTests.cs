// Checks that grounding is decided by verified citations alone (fail closed).
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Unit.Persistence;

public sealed class AnswerTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 3, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void IsGrounded_RequiresAtLeastOneVerifiedCitation()
    {
        var grounded = Answer.Create(1, "Exhaust smell?", "{}", verifiedCitationCount: 1, droppedCitationCount: 3, Now);
        var notGrounded = Answer.Create(1, "Exhaust smell?", "{}", verifiedCitationCount: 0, droppedCitationCount: 3, Now);

        Assert.True(grounded.IsGrounded);
        Assert.False(notGrounded.IsGrounded);
    }

    [Fact]
    public void Create_RejectsNegativeCitationCounts()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Answer.Create(1, "q", "{}", -1, 0, Now));
        Assert.Throws<ArgumentOutOfRangeException>(() => Answer.Create(1, "q", "{}", 0, -1, Now));
    }

    [Fact]
    public void Create_RejectsBlankQuestionOrAnswer()
    {
        Assert.Throws<ArgumentException>(() => Answer.Create(1, " ", "{}", 0, 0, Now));
        Assert.Throws<ArgumentException>(() => Answer.Create(1, "q", "", 0, 0, Now));
    }
}
