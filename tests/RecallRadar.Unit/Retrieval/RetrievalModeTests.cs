// Checks mode parsing from user input and which modes need embeddings.
using RecallRadar.Domain.Retrieval;

namespace RecallRadar.Unit.Retrieval;

public sealed class RetrievalModeTests
{
    [Theory]
    [InlineData("dense", RetrievalMode.Dense)]
    [InlineData(" Sparse ", RetrievalMode.Sparse)]
    [InlineData("HYBRID", RetrievalMode.Hybrid)]
    public void TryParse_AcceptsAnyCasing(string text, RetrievalMode expected)
    {
        Assert.Equal(expected, RetrievalModes.TryParse(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("fuzzy")]
    [InlineData("4")]
    public void TryParse_ReturnsNullForUnknownInputInsteadOfGuessing(string? text)
    {
        Assert.Null(RetrievalModes.TryParse(text));
    }

    [Fact]
    public void RequiresEmbeddings_OnlySparseWorksWithoutThem()
    {
        Assert.True(RetrievalMode.Dense.RequiresEmbeddings());
        Assert.True(RetrievalMode.Hybrid.RequiresEmbeddings());
        Assert.False(RetrievalMode.Sparse.RequiresEmbeddings());
    }
}
