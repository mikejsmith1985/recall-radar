// Checks that a provider outage is reported without quoting anything the provider sent back.
using RecallRadar.Retrieval.Embeddings;

namespace RecallRadar.Unit.Embeddings;

public sealed class EmbeddingProviderUnavailableExceptionTests
{
    [Fact]
    public void FromTransportFailure_KeepsTheCauseAndNamesTheFallback()
    {
        var cause = new HttpRequestException("connection refused");

        var failure = EmbeddingProviderUnavailableException.FromTransportFailure(cause);

        Assert.Same(cause, failure.InnerException);
        Assert.Contains("could not be reached", failure.Message, StringComparison.Ordinal);
        Assert.Contains("mode=sparse", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void FromStatus_NamesTheCodeButNeverTheBody()
    {
        var failure = EmbeddingProviderUnavailableException.FromStatus(502);

        Assert.Contains("502", failure.Message, StringComparison.Ordinal);
        Assert.Contains(EmbeddingProviderUnavailableException.FallbackAdvice, failure.Message, StringComparison.Ordinal);
        Assert.Null(failure.InnerException);
    }

    [Fact]
    public void FromTransportFailure_RejectsANullCause()
    {
        Assert.Throws<ArgumentNullException>(() => EmbeddingProviderUnavailableException.FromTransportFailure(null!));
    }

    [Fact]
    public void AnOutageIsNotTheSameTypeAsAMissingKey()
    {
        // The two mean different things to a reader: one is a configuration gap that stays until
        // someone adds a key, the other is temporary. They must not be caught as one.
        Assert.False(typeof(EmbeddingsUnavailableException)
            .IsAssignableFrom(typeof(EmbeddingProviderUnavailableException)));
    }
}
