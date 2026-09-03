// A repeatable stand-in for a real embedding provider, so retrieval can be exercised without a key.
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.AI;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Retrieval.Embeddings;

/// <summary>
/// Turns text into a stable unit vector by hashing it and expanding the hash. Same text in, same
/// vector out, on any machine and in any run, which is what lets the tests and the browser fixture
/// assert an exact retrieval order.
/// </summary>
/// <remarks>
/// This is NOT semantic. Two complaints describing the same fault in different words get unrelated
/// vectors, so nearest-neighbour results here mean nothing about meaning. It exists to prove the
/// plumbing — that vectors are stored, indexed, ordered by cosine distance and fused with the
/// keyword ranks — while the Voyage key is pending. Never register it in production: the
/// <see cref="NullEmbeddingGenerator"/> is the honest no-key fallback, because it fails loudly
/// instead of returning confident nonsense.
/// </remarks>
public sealed class DeterministicEmbeddingGenerator : IEmbeddingGenerator<string, Embedding<float>>
{
    /// <summary>Matches the stored column width, so a fixture vector is storable without conversion.</summary>
    public const int Dimensions = DocumentChunk.EmbeddingDimensions;

    /// <summary>Produces one unit vector per input, preserving input order.</summary>
    public Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        var embeddings = new GeneratedEmbeddings<Embedding<float>>(
            values.Select(value => new Embedding<float>(BuildUnitVector(value))));
        return Task.FromResult(embeddings);
    }

    /// <summary>Lets a caller recognise the stand-in through the framework's service-discovery seam.</summary>
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
    }

    public void Dispose()
    {
        // Nothing to release: the vector is computed from the text and no connection is held.
    }

    /// <summary>
    /// Expands the text's SHA-256 hash into <see cref="Dimensions"/> components, then scales them to
    /// length one. The hash is seeded per block so the whole vector varies with the text rather than
    /// repeating the same thirty-two bytes.
    /// </summary>
    private static ReadOnlyMemory<float> BuildUnitVector(string value)
    {
        var components = new float[Dimensions];
        var textBytes = Encoding.UTF8.GetBytes(value ?? string.Empty);
        var blockSeed = new byte[textBytes.Length + sizeof(int)];
        textBytes.CopyTo(blockSeed, 0);

        for (var blockIndex = 0; blockIndex * SHA256.HashSizeInBytes < Dimensions; blockIndex++)
        {
            BitConverter.TryWriteBytes(blockSeed.AsSpan(textBytes.Length), blockIndex);
            var block = SHA256.HashData(blockSeed);
            FillFromBlock(components, blockIndex * SHA256.HashSizeInBytes, block);
        }

        return Normalise(components);
    }

    /// <summary>Maps each hash byte to a component in [-1, 1), so the vector points in every direction.</summary>
    private static void FillFromBlock(float[] components, int offset, ReadOnlySpan<byte> block)
    {
        const float ByteMidpoint = 128f;
        for (var index = 0; index < block.Length && offset + index < components.Length; index++)
        {
            components[offset + index] = (block[index] - ByteMidpoint) / ByteMidpoint;
        }
    }

    /// <summary>Scales to unit length so cosine distance depends on direction alone.</summary>
    private static ReadOnlyMemory<float> Normalise(float[] components)
    {
        var length = MathF.Sqrt(components.Sum(component => component * component));
        if (length == 0f)
        {
            // Only reachable if every hash byte landed exactly on the midpoint. Point at one axis
            // rather than return a zero vector, which has no direction for cosine distance to use.
            components[0] = 1f;
            return components;
        }

        for (var index = 0; index < components.Length; index++)
        {
            components[index] /= length;
        }

        return components;
    }
}
