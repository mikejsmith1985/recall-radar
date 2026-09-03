// A retrievable slice of a source document, carrying its embedding and full-text search vector.
using NpgsqlTypes;
using Pgvector;

namespace RecallRadar.Retrieval.Persistence;

/// <summary>
/// The unit of retrieval. Complaints fit in one chunk; long investigation summaries are split.
/// <see cref="SearchText"/> is a database-generated tsvector and is never set from code.
/// </summary>
public sealed class DocumentChunk
{
    /// <summary>Voyage AI voyage-3.5 output width. Changing it means a new migration and a full re-embed.</summary>
    public const int EmbeddingDimensions = 1024;

    public long Id { get; private set; }
    public long DocumentId { get; private set; }
    public SourceDocument? Document { get; private set; }
    public int Ordinal { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public Vector? Embedding { get; private set; }
    public NpgsqlTsVector? SearchText { get; private set; }

    private DocumentChunk() { }

    /// <summary>Creates a chunk. Ordinal is zero-based so chunk order survives a round trip through the database.</summary>
    public static DocumentChunk Create(long documentId, int ordinal, string text)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        return new DocumentChunk { DocumentId = documentId, Ordinal = ordinal, Text = text };
    }

    /// <summary>Attaches an embedding, rejecting any vector that is not the configured width.</summary>
    public void SetEmbedding(Vector embedding)
    {
        ArgumentNullException.ThrowIfNull(embedding);
        if (embedding.Memory.Length != EmbeddingDimensions)
        {
            throw new ArgumentException(
                $"Expected {EmbeddingDimensions} dimensions but received {embedding.Memory.Length}.", nameof(embedding));
        }

        Embedding = embedding;
    }
}
