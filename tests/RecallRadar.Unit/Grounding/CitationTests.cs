// Checks the two presence flags a citation exposes before any verification happens.
using RecallRadar.Domain.Grounding;

namespace RecallRadar.Unit.Grounding;

public sealed class CitationTests
{
    [Theory]
    [InlineData("doc-1", "quote", true, true)]
    [InlineData("", "quote", false, true)]
    [InlineData("doc-1", "  ", true, false)]
    [InlineData(" ", "", false, false)]
    public void PresenceFlagsReflectBlankValues(string documentId, string quote, bool expectHasId, bool expectHasQuote)
    {
        var citation = new Citation(documentId, quote);

        Assert.Equal(expectHasId, citation.HasDocumentId);
        Assert.Equal(expectHasQuote, citation.HasQuote);
    }

    [Fact]
    public void CitationsWithTheSameValuesAreEqual()
    {
        Assert.Equal(new Citation("doc-1", "quote"), new Citation("doc-1", "quote"));
        Assert.NotEqual(new Citation("doc-1", "quote"), new Citation("doc-2", "quote"));
    }
}
