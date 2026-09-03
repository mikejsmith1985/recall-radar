// Raised when dense retrieval is asked for but no embeddings exist to search.
namespace RecallRadar.Retrieval.Embeddings;

/// <summary>
/// Dense and hybrid search need vectors. Two things can leave them missing: no provider is
/// configured, or a vehicle's chunks were stored before one was. Both are ordinary states rather
/// than faults, so this carries a <see cref="Reason"/> the API renders as a 409 problem detail and
/// the command line prints after <c>error:</c>.
/// </summary>
public sealed class EmbeddingsUnavailableException : Exception
{
    /// <summary>The environment variable the Forge Vault fills in. Only the name is ever named; never a value.</summary>
    public const string ApiKeyVariable = "VOYAGE_API_KEY";

    /// <summary>The wording the command-line contract expects after <c>error:</c>.</summary>
    public const string NoKeyReason = $"{ApiKeyVariable} not set";

    /// <summary>Why embeddings are unavailable, in words a caller can act on.</summary>
    public string Reason { get; }

    /// <summary>Creates the exception with a reason that becomes both the message and the problem detail.</summary>
    public EmbeddingsUnavailableException(string reason)
        : base(reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        Reason = reason;
    }

    /// <summary>No embedding provider is configured, so nothing can be embedded or searched by vector.</summary>
    public static EmbeddingsUnavailableException NoProviderConfigured() => new(NoKeyReason);

    /// <summary>The vehicle is loaded but its chunks carry no vectors yet.</summary>
    public static EmbeddingsUnavailableException NoEmbeddedChunks(string vehicleDisplayName) => new(
        $"No chunk for '{vehicleDisplayName}' has an embedding yet. Run the embed verb to back-fill, or search with mode=sparse.");
}
