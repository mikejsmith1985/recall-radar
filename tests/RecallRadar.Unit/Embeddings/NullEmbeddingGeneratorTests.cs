// Checks the no-key fallback refuses rather than inventing vectors.
using Microsoft.Extensions.AI;
using RecallRadar.Retrieval.Embeddings;

namespace RecallRadar.Unit.Embeddings;

public sealed class NullEmbeddingGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_RefusesWithTheReasonTheOperatorNeeds()
    {
        using var generator = new NullEmbeddingGenerator();

        var failure = await Assert.ThrowsAsync<EmbeddingsUnavailableException>(
            () => generator.GenerateAsync(["text"], cancellationToken: CancellationToken.None));

        Assert.Equal(EmbeddingsUnavailableException.NoKeyReason, failure.Reason);
    }

    [Fact]
    public async Task GenerateAsync_RefusesEvenForAnEmptyBatchSoTheStateIsNeverAmbiguous()
    {
        using var generator = new NullEmbeddingGenerator();

        await Assert.ThrowsAsync<EmbeddingsUnavailableException>(
            () => generator.GenerateAsync([], cancellationToken: CancellationToken.None));
    }

    [Fact]
    public void GetService_IdentifiesItselfSoCallersCanAskBeforeTrying()
    {
        using var generator = new NullEmbeddingGenerator();

        Assert.Same(generator, generator.GetService(typeof(NullEmbeddingGenerator)));
        Assert.Same(generator, generator.GetService(typeof(IEmbeddingGenerator<string, Embedding<float>>)));
        Assert.Null(generator.GetService(typeof(DeterministicEmbeddingGenerator)));
    }
}
