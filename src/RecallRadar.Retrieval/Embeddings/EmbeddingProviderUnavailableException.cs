// Raised when the embedding provider is configured but cannot be reached or fails a request.
namespace RecallRadar.Retrieval.Embeddings;

/// <summary>
/// Distinct from <see cref="EmbeddingsUnavailableException"/>, which means no key is configured.
/// This one means the key is fine and the provider is down, timing out, or erroring. The caller
/// renders it as a temporary failure, because the remedy is to retry or fall back to keyword
/// search rather than to change configuration.
/// </summary>
public sealed class EmbeddingProviderUnavailableException(string reason, Exception? innerException = null)
    : Exception(reason, innerException)
{
    /// <summary>What the caller can tell a reader, with the remedy that always works.</summary>
    public const string FallbackAdvice = "Retry, or search with mode=sparse, which needs no provider.";

    /// <summary>Wraps a transport-level failure without quoting the response, which can echo the key.</summary>
    public static EmbeddingProviderUnavailableException FromTransportFailure(Exception failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return new EmbeddingProviderUnavailableException(
            $"The embedding provider could not be reached. {FallbackAdvice}", failure);
    }

    /// <summary>Wraps an unsuccessful status, naming the code but never the body.</summary>
    public static EmbeddingProviderUnavailableException FromStatus(int statusCode) =>
        new($"The embedding provider answered {statusCode}. {FallbackAdvice}");
}
