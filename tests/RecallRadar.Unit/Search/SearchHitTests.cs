// Checks the snippet rules, which are what a reader actually sees in a result list.
using RecallRadar.Retrieval.Search;

namespace RecallRadar.Unit.Search;

public sealed class SearchHitTests
{
    [Fact]
    public void BuildSnippet_LeavesShortTextAloneApartFromCollapsingWhitespace()
    {
        var snippet = SearchHit.BuildSnippet("  The contact owns a 2013 Ford Explorer.\n\n  Exhaust odor. ");

        Assert.Equal("The contact owns a 2013 Ford Explorer. Exhaust odor.", snippet);
    }

    [Fact]
    public void BuildSnippet_CutsLongTextAtAWordBoundaryAndMarksTheCut()
    {
        var words = string.Join(' ', Enumerable.Repeat("complaint", 200));

        var snippet = SearchHit.BuildSnippet(words);

        Assert.True(snippet.Length <= SearchHit.SnippetLength + 1);
        Assert.EndsWith("…", snippet, StringComparison.Ordinal);
        Assert.DoesNotContain("complai…", snippet, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSnippet_HandlesTextWithNoSpacesWithoutRunningPastTheLimit()
    {
        var unbroken = new string('x', SearchHit.SnippetLength * 2);

        var snippet = SearchHit.BuildSnippet(unbroken);

        Assert.Equal(SearchHit.SnippetLength + 1, snippet.Length);
        Assert.EndsWith("…", snippet, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildSnippet_RejectsNullRatherThanReturningSomethingMisleading()
    {
        Assert.Throws<ArgumentNullException>(() => SearchHit.BuildSnippet(null!));
    }
}
