// Checks that a chunk only accepts embeddings of the configured width.
using Pgvector;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Unit.Persistence;

public sealed class DocumentChunkTests
{
    [Fact]
    public void SetEmbedding_AcceptsVectorOfConfiguredWidth()
    {
        var chunk = DocumentChunk.Create(documentId: 7, ordinal: 0, text: "Exhaust odor in the cabin.");
        var embedding = new Vector(new float[DocumentChunk.EmbeddingDimensions]);

        chunk.SetEmbedding(embedding);

        Assert.Same(embedding, chunk.Embedding);
    }

    [Fact]
    public void SetEmbedding_RejectsVectorOfWrongWidth()
    {
        var chunk = DocumentChunk.Create(documentId: 7, ordinal: 0, text: "Exhaust odor in the cabin.");
        var wrongWidth = new Vector(new float[DocumentChunk.EmbeddingDimensions - 1]);

        var error = Assert.Throws<ArgumentException>(() => chunk.SetEmbedding(wrongWidth));

        Assert.Contains(DocumentChunk.EmbeddingDimensions.ToString(), error.Message);
        Assert.Null(chunk.Embedding);
    }

    [Fact]
    public void Create_RejectsNegativeOrdinalAndBlankText()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => DocumentChunk.Create(1, -1, "text"));
        Assert.Throws<ArgumentException>(() => DocumentChunk.Create(1, 0, " "));
    }
}
