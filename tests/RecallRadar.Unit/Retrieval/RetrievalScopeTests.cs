// Checks the scope name a caller may ask for. Which kinds it admits lives in ScopedKindsTests.
using RecallRadar.Domain.Retrieval;

namespace RecallRadar.Unit.Retrieval;

public sealed class RetrievalScopeTests
{
    [Theory]
    [InlineData("all", RetrievalScope.All)]
    [InlineData("ALL", RetrievalScope.All)]
    [InlineData("campaigns", RetrievalScope.Campaigns)]
    [InlineData(" Campaigns ", RetrievalScope.Campaigns)]
    public void TryParse_ReadsAKnownScopeWhateverTheCasing(string text, RetrievalScope expected)
    {
        Assert.Equal(expected, RetrievalScopes.TryParse(text));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("recalls")]
    public void TryParse_ReturnsNullSoTheCallerChoosesTheDefault(string? text)
    {
        Assert.Null(RetrievalScopes.TryParse(text));
    }

    [Fact]
    public void DoesNotAcceptTheNumberBehindAName()
    {
        // Enum.TryParse also accepts the underlying number, which would make "1" a silent alias
        // for the first member: an input the contract never documented.
        Assert.Null(RetrievalScopes.TryParse("1"));
    }
}
