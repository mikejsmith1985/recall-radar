// Checks the embed verb refuses without a provider, and that its batch size matches Voyage's limit.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RecallRadar.Ingest.Commands;
using RecallRadar.Retrieval.Embeddings;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Unit.Commands;

public sealed class EmbedCommandTests
{
    [Fact]
    public async Task EmbedAsync_RefusesBeforeReadingAnyRowWhenNoProviderIsConfigured()
    {
        // The context is deliberately unusable: if the guard were to run after a query, this test
        // would fail with a connection error instead of the message the contract promises.
        using var command = BuildCommandWithUnusableDatabase(new NullEmbeddingGenerator());

        var failure = await Assert.ThrowsAsync<EmbeddingsUnavailableException>(
            () => command.Command.EmbedAsync(vehicleDisplayName: null, CancellationToken.None));

        Assert.Equal(EmbeddingsUnavailableException.NoKeyReason, failure.Reason);
    }

    [Fact]
    public async Task EmbedAsync_RefusesForANamedVehicleToo()
    {
        using var command = BuildCommandWithUnusableDatabase(new NullEmbeddingGenerator());

        await Assert.ThrowsAsync<EmbeddingsUnavailableException>(
            () => command.Command.EmbedAsync("2013 Explorer Sport", CancellationToken.None));
    }

    [Fact]
    public void BatchSize_MatchesTheProviderLimitSoNoRequestIsEverRejectedForSize()
    {
        Assert.Equal(VoyageEmbeddingGenerator.MaxBatchSize, EmbedCommand.BatchSize);
        Assert.InRange(EmbedCommand.BatchSize, 1, VoyageEmbeddingGenerator.MaxBatchSize);
    }

    private static CommandUnderTest BuildCommandWithUnusableDatabase(NullEmbeddingGenerator generator)
    {
        // Port 1 refuses instantly, so any query would throw rather than hang.
        const string unreachable = "Host=127.0.0.1;Port=1;Database=nowhere;Username=nobody;Password=none;Timeout=1";
        var options = RecallRadarDbContextFactory
            .Configure(new DbContextOptionsBuilder<RecallRadarDbContext>(), unreachable)
            .Options;
        var database = new RecallRadarDbContext(options);
        return new CommandUnderTest(
            new EmbedCommand(database, generator, NullLogger<EmbedCommand>.Instance), database, generator);
    }

    private sealed record CommandUnderTest(EmbedCommand Command, RecallRadarDbContext Database, NullEmbeddingGenerator Generator) : IDisposable
    {
        public void Dispose()
        {
            Database.Dispose();
            Generator.Dispose();
        }
    }
}
