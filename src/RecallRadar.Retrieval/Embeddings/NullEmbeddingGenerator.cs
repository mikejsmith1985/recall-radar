// The registered generator when no embedding key exists: it refuses rather than inventing vectors.
using Microsoft.Extensions.AI;

namespace RecallRadar.Retrieval.Embeddings;

/// <summary>
/// Stands in the container when no provider is configured, and throws
/// <see cref="EmbeddingsUnavailableException"/> on every call.
/// </summary>
/// <remarks>
/// Failing loudly is the point. Keyword search needs no vectors and keeps working; only the paths
/// that genuinely require embeddings stop, and they stop with a reason the caller can show a user.
/// Returning zeros or random vectors instead would let dense search appear to work while ranking
/// results by nothing at all, which is worse than an error because it is believed.
/// </remarks>
public sealed class NullEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    /// <summary>Throws, because there is no provider to generate anything with.</summary>
    /// <exception cref="EmbeddingsUnavailableException">Always.</exception>
    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default) =>
        throw EmbeddingsUnavailableException.NoProviderConfigured();

    /// <summary>
    /// Answers for its own type so a caller can ask the container whether embeddings are available
    /// without catching an exception to find out.
    /// </summary>
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
    }

    public void Dispose()
    {
        // Nothing to release: no client was ever created.
    }
}
