// Checks the question-to-query conversion that decides whether a real question matches anything.
using RecallRadar.Domain.Retrieval;

namespace RecallRadar.Unit.Retrieval;

public sealed class KeywordQueryTests
{
    private const string RealQuestion =
        "I get a strong exhaust smell inside the cabin when I accelerate. Is this a known problem?";

    [Fact]
    public void BuildAnyTermQuery_JoinsTermsWithOrSoAQuestionCanMatch()
    {
        // Joined with AND, this question matches no record in the corpus at all. That is the bug
        // this exists to prevent, and it was only visible when a live question was asked.
        var query = KeywordQuery.BuildAnyTermQuery(RealQuestion);

        Assert.NotNull(query);
        Assert.Contains("|", query, StringComparison.Ordinal);
        Assert.DoesNotContain("&", query, StringComparison.Ordinal);
        Assert.Contains("exhaust", query, StringComparison.Ordinal);
        Assert.Contains("cabin", query, StringComparison.Ordinal);
        Assert.Contains("accelerate", query, StringComparison.Ordinal);
    }

    [Fact]
    public void ExtractTerms_DropsShortAndUninformativeWords()
    {
        var terms = KeywordQuery.ExtractTerms(RealQuestion);

        Assert.DoesNotContain("the", terms);
        Assert.DoesNotContain("this", terms);
        Assert.DoesNotContain("known", terms);
        Assert.DoesNotContain("problem", terms);
        Assert.DoesNotContain("get", terms);
        Assert.Contains("exhaust", terms);
    }

    [Fact]
    public void ExtractTerms_StripsPunctuationSoTheDatabaseNeverParsesItAsSyntax()
    {
        // A tsquery is parsed by PostgreSQL. An ampersand or quote left in would be read as an
        // operator and fail the query, or worse, change what it means.
        var terms = KeywordQuery.ExtractTerms("brakes & steering | 'squeal' (loud) !urgent");

        Assert.All(terms, term => Assert.True(term.All(char.IsLetterOrDigit)));
        Assert.Contains("brakes", terms);
        Assert.Contains("steering", terms);
        Assert.Contains("squeal", terms);
    }

    [Fact]
    public void ExtractTerms_KeepsTheFirstOccurrenceOfARepeatedWordOnly()
    {
        var terms = KeywordQuery.ExtractTerms("exhaust exhaust EXHAUST odor");

        Assert.Equal(["exhaust", "odor"], terms);
    }

    [Fact]
    public void ExtractTerms_StopsAtTheTermCap()
    {
        var manyWords = string.Join(' ', Enumerable.Range(0, 100).Select(index => $"term{index}"));

        var terms = KeywordQuery.ExtractTerms(manyWords);

        Assert.Equal(KeywordQuery.MaximumTerms, terms.Count);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("is it a to")]
    [InlineData("!!! ??? ...")]
    public void BuildAnyTermQuery_ReturnsNullWhenNothingUsableRemains(string? question)
    {
        // Null rather than an empty query: an empty tsquery would match everything, and returning
        // the whole corpus for a meaningless question is worse than returning nothing.
        Assert.Null(KeywordQuery.BuildAnyTermQuery(question));
    }

    [Fact]
    public void ExtractTerms_KeepsDigitsSoCampaignAndPartNumbersStillMatch()
    {
        var terms = KeywordQuery.ExtractTerms("recall 17V001000 affects the toe link");

        Assert.Contains("17V001000", terms);
        Assert.Contains("link", terms);
    }
}
