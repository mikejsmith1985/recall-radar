// Checks the exception that turns a missing embedding provider into a message a caller can act on.
using RecallRadar.Retrieval.Embeddings;

namespace RecallRadar.Unit.Embeddings;

public sealed class EmbeddingsUnavailableExceptionTests
{
    [Fact]
    public void NoProviderConfigured_NamesTheVariableTheOperatorHasToSet()
    {
        var failure = EmbeddingsUnavailableException.NoProviderConfigured();

        Assert.Equal(EmbeddingsUnavailableException.NoKeyReason, failure.Reason);
        Assert.Equal(EmbeddingsUnavailableException.NoKeyReason, failure.Message);
        Assert.Contains(EmbeddingsUnavailableException.ApiKeyVariable, failure.Reason);
    }

    [Fact]
    public void NoEmbeddedChunks_SaysWhichVehicleAndWhatToRunNext()
    {
        var failure = EmbeddingsUnavailableException.NoEmbeddedChunks("2013 Explorer Sport");

        Assert.Contains("2013 Explorer Sport", failure.Reason);
        Assert.Contains("embed", failure.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Reason_IsCarriedSoTheApiCanRenderItAsAProblemDetail()
    {
        const string reason = "Embeddings are unavailable; use mode=sparse";

        var failure = new EmbeddingsUnavailableException(reason);

        Assert.Equal(reason, failure.Reason);
        Assert.Equal(reason, failure.Message);
    }

    [Fact]
    public void Reason_NeverContainsKeyMaterialBecauseOnlyTheVariableNameIsKnown()
    {
        var failure = EmbeddingsUnavailableException.NoProviderConfigured();

        Assert.DoesNotContain("pa-", failure.Reason, StringComparison.Ordinal);
        Assert.DoesNotContain("=", failure.Reason, StringComparison.Ordinal);
    }
}
