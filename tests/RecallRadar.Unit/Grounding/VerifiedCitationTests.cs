// Checks that verified offsets slice the original body faithfully and reject bodies they do not fit.
using RecallRadar.Domain.Grounding;

namespace RecallRadar.Unit.Grounding;

public sealed class VerifiedCitationTests
{
    private const string Body = "Front brake hose failure reported.";

    [Fact]
    public void SliceOf_ReturnsTheMatchedPassage()
    {
        var verified = new VerifiedCitation(new Citation("inv-1", "brake hose"), 6, 16);

        Assert.Equal("brake hose", verified.SliceOf(Body));
        Assert.Equal(10, verified.Length);
    }

    [Theory]
    [InlineData(-1, 5)]
    [InlineData(0, 999)]
    [InlineData(10, 4)]
    public void SliceOf_RejectsOffsetsOutsideTheBody(int start, int end)
    {
        var verified = new VerifiedCitation(new Citation("inv-1", "x"), start, end);

        Assert.Throws<ArgumentOutOfRangeException>(() => verified.SliceOf(Body));
    }
}
