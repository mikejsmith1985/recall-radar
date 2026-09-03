// Pins down exactly what counts as a verbatim quote: whitespace may differ, nothing else may.
using RecallRadar.Domain.Grounding;

namespace RecallRadar.Unit.Grounding;

public sealed class QuoteVerifierTests
{
    private const string Body =
        "The contact owns a 2013 Ford Explorer.\n\nWhile driving, an exhaust   odor entered the\r\n  passenger cabin. The dealer was notified.";

    [Fact]
    public void Verify_ExactQuotePassesWithOffsetsIntoTheOriginalBody()
    {
        var result = QuoteVerifier.Verify("The dealer was notified.", Body);

        Assert.True(result.IsVerified);
        Assert.Equal(string.Empty, result.Reason);
        Assert.Equal("The dealer was notified.", Body[result.StartOffset..result.EndOffset]);
    }

    [Fact]
    public void Verify_QuoteWithDifferentLineWrappingPasses()
    {
        var rewrapped = "an exhaust odor entered the passenger cabin";

        var result = QuoteVerifier.Verify(rewrapped, Body);

        Assert.True(result.IsVerified);
        Assert.Equal("an exhaust   odor entered the\r\n  passenger cabin", Body[result.StartOffset..result.EndOffset]);
    }

    [Fact]
    public void Verify_QuoteWithDifferentCaseFails()
    {
        var result = QuoteVerifier.Verify("the dealer was notified.", Body);

        Assert.False(result.IsVerified);
        Assert.Equal(QuoteVerifier.NotFoundReason, result.Reason);
        Assert.Equal(-1, result.StartOffset);
    }

    [Fact]
    public void Verify_ParaphraseFails()
    {
        var result = QuoteVerifier.Verify("exhaust fumes came into the cabin", Body);

        Assert.False(result.IsVerified);
        Assert.Equal(QuoteVerifier.NotFoundReason, result.Reason);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n\t ")]
    public void Verify_EmptyQuoteFailsWithItsOwnReason(string quote)
    {
        var result = QuoteVerifier.Verify(quote, Body);

        Assert.False(result.IsVerified);
        Assert.Equal(QuoteVerifier.EmptyQuoteReason, result.Reason);
    }

    [Fact]
    public void Verify_LeadingWhitespaceInBodyDoesNotShiftOffsets()
    {
        const string padded = "   \n Exhaust odor. ";

        var result = QuoteVerifier.Verify("Exhaust odor.", padded);

        Assert.True(result.IsVerified);
        Assert.Equal("Exhaust odor.", padded[result.StartOffset..result.EndOffset]);
    }

    [Theory]
    [InlineData("  a   b\n\nc ", "a b c")]
    [InlineData("\t\r\n", "")]
    [InlineData("no-change", "no-change")]
    public void NormaliseWhitespace_CollapsesRunsAndTrims(string input, string expected)
    {
        Assert.Equal(expected, QuoteVerifier.NormaliseWhitespace(input));
    }
}
