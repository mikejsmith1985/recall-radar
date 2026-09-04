// Checks the stand-in generator is stable, correctly shaped, and honest about what it is not.
using Microsoft.Extensions.AI;
using RecallRadar.Retrieval.Embeddings;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Unit.Embeddings;

public sealed class DeterministicEmbeddingGeneratorTests
{
    private const string ExhaustComplaint = "Exhaust odor enters the passenger cabin under acceleration.";
    private const string BrakeComplaint = "The brake pedal went to the floor without warning.";
    private const float UnitLengthTolerance = 1e-4f;

    [Fact]
    public async Task GenerateAsync_ReturnsOneEmbeddingPerInputInOrder()
    {
        var generator = new DeterministicEmbeddingGenerator();

        var embeddings = await generator.GenerateAsync([ExhaustComplaint, BrakeComplaint], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, embeddings.Count);
        Assert.All(embeddings, embedding => Assert.Equal(DocumentChunk.EmbeddingDimensions, embedding.Vector.Length));
    }

    [Fact]
    public async Task GenerateAsync_GivesTheSameTextTheSameVectorEveryTime()
    {
        var firstRun = await new DeterministicEmbeddingGenerator()
            .GenerateAsync([ExhaustComplaint], cancellationToken: TestContext.Current.CancellationToken);
        var secondRun = await new DeterministicEmbeddingGenerator()
            .GenerateAsync([ExhaustComplaint], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(firstRun[0].Vector.ToArray(), secondRun[0].Vector.ToArray());
    }

    [Fact]
    public async Task GenerateAsync_GivesDifferentTextDifferentVectors()
    {
        var embeddings = await new DeterministicEmbeddingGenerator()
            .GenerateAsync([ExhaustComplaint, BrakeComplaint], cancellationToken: TestContext.Current.CancellationToken);

        Assert.NotEqual(embeddings[0].Vector.ToArray(), embeddings[1].Vector.ToArray());
    }

    [Fact]
    public async Task GenerateAsync_ProducesUnitVectorsSoCosineDistanceBehaves()
    {
        var embeddings = await new DeterministicEmbeddingGenerator()
            .GenerateAsync([ExhaustComplaint, BrakeComplaint], cancellationToken: TestContext.Current.CancellationToken);

        foreach (var embedding in embeddings)
        {
            var length = MathF.Sqrt(embedding.Vector.ToArray().Sum(component => component * component));
            Assert.InRange(length, 1f - UnitLengthTolerance, 1f + UnitLengthTolerance);
        }
    }

    [Fact]
    public async Task GenerateAsync_AcceptsAnEmptyBatchWithoutComplaint()
    {
        var embeddings = await new DeterministicEmbeddingGenerator()
            .GenerateAsync([], cancellationToken: TestContext.Current.CancellationToken);

        Assert.Empty(embeddings);
    }

    [Fact]
    public void GetService_AnswersForItsOwnTypeSoCallersCanRecogniseTheStandIn()
    {
        var generator = new DeterministicEmbeddingGenerator();

        Assert.Same(generator, generator.GetService(typeof(DeterministicEmbeddingGenerator)));
        Assert.Null(generator.GetService(typeof(NullEmbeddingGenerator)));
    }
}
