// Checks how a trim scope is read from what a caller typed.
using RecallRadar.Domain.Retrieval;

namespace RecallRadar.Unit.Retrieval;

public sealed class TrimScopeTests
{
    [Theory]
    [InlineData("thisTrim", TrimScope.ThisTrim)]
    [InlineData("ThisTrim", TrimScope.ThisTrim)]
    [InlineData("  allTrims  ", TrimScope.AllTrims)]
    [InlineData("ALLTRIMS", TrimScope.AllTrims)]
    public void ReadsAScopeWhateverTheCasingAndSpacing(string text, TrimScope expected)
    {
        Assert.Equal(expected, TrimScopes.TryParse(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("everything")]
    [InlineData("1")]
    public void GivesNothingBackForBlankOrUnknownInput(string? text)
    {
        // Null rather than a default, so the caller decides whether that is an error or a fall-back.
        Assert.Null(TrimScopes.TryParse(text));
    }

    [Fact]
    public void IsWrittenAsItsNameSoAStoredScopeIsReadableAYearLater()
    {
        var json = System.Text.Json.JsonSerializer.Serialize(TrimScope.ThisTrim);

        Assert.Equal("\"ThisTrim\"", json);
    }
}
