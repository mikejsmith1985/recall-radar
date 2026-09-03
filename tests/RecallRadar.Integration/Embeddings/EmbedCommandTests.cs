// Runs the back-fill against a real pgvector container, using the deterministic stand-in generator.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RecallRadar.Ingest.Commands;
using RecallRadar.Retrieval.Embeddings;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Integration.Embeddings;

[Collection(PostgresCollection.Name)]
public sealed class EmbedCommandTests(PostgresFixture postgres)
{
    private const string ExplorerName = "2013 Explorer Sport (embed test)";
    private const string RaptorName = "2014 F-150 SVT Raptor (embed test)";

    [Fact]
    public async Task EmbedAsync_GivesEveryPendingChunkAVectorOfTheStoredWidth()
    {
        var vehicleId = await SeedVehicleWithChunksAsync(ExplorerName, "EXPLORER", 2013, chunkCount: 5);

        var report = await RunEmbedAsync(ExplorerName);

        Assert.Equal(5, report.Embedded);
        Assert.Equal(0, report.Remaining);
        Assert.True(report.IsComplete);
        await using var readContext = postgres.CreateContext();
        var stored = await readContext.DocumentChunks
            .Where(chunk => chunk.Document!.VehicleId == vehicleId)
            .ToListAsync(CancellationToken.None);
        Assert.All(stored, chunk =>
        {
            Assert.NotNull(chunk.Embedding);
            Assert.Equal(DocumentChunk.EmbeddingDimensions, chunk.Embedding!.Memory.Length);
        });
    }

    [Fact]
    public async Task EmbedAsync_EmbedsNothingOnASecondRunBecauseNoChunkIsPending()
    {
        await SeedVehicleWithChunksAsync($"{ExplorerName} second run", "EXPLORER", 2011, chunkCount: 3);
        await RunEmbedAsync($"{ExplorerName} second run");

        var secondRun = await RunEmbedAsync($"{ExplorerName} second run");

        Assert.Equal(0, secondRun.Embedded);
        Assert.Equal(0, secondRun.Remaining);
    }

    [Fact]
    public async Task EmbedAsync_LeavesAnotherVehiclesChunksAloneWhenOneIsNamed()
    {
        await SeedVehicleWithChunksAsync(ExplorerName + " scoped", "EXPLORER", 2012, chunkCount: 2);
        var untouchedVehicleId = await SeedVehicleWithChunksAsync(RaptorName, "F-150 SUPER CREW", 2014, chunkCount: 4);

        await RunEmbedAsync(ExplorerName + " scoped");

        await using var readContext = postgres.CreateContext();
        var untouched = await readContext.DocumentChunks
            .Where(chunk => chunk.Document!.VehicleId == untouchedVehicleId)
            .ToListAsync(CancellationToken.None);
        Assert.Equal(4, untouched.Count);
        Assert.All(untouched, chunk => Assert.Null(chunk.Embedding));
    }

    [Fact]
    public async Task EmbedAsync_RefusesWhenNoProviderIsConfiguredEvenWithChunksWaiting()
    {
        await SeedVehicleWithChunksAsync($"{ExplorerName} no key", "EXPLORER", 2010, chunkCount: 2);
        await using var database = postgres.CreateContext();
        using var generator = new NullEmbeddingGenerator();
        var command = new EmbedCommand(database, generator, NullLogger<EmbedCommand>.Instance);

        var failure = await Assert.ThrowsAsync<EmbeddingsUnavailableException>(
            () => command.EmbedAsync($"{ExplorerName} no key", CancellationToken.None));

        Assert.Equal(EmbeddingsUnavailableException.NoKeyReason, failure.Reason);
    }

    [Fact]
    public async Task EmbedAsync_EmbedsMoreThanOneBatchInASingleRun()
    {
        var manyChunks = EmbedCommand.BatchSize + 3;
        await SeedVehicleWithChunksAsync($"{ExplorerName} batching", "EXPLORER", 2009, manyChunks);

        var report = await RunEmbedAsync($"{ExplorerName} batching");

        Assert.Equal(manyChunks, report.Embedded);
        Assert.Equal(0, report.Remaining);
    }

    private async Task<EmbedReport> RunEmbedAsync(string vehicleDisplayName)
    {
        await using var database = postgres.CreateContext();
        using var generator = new DeterministicEmbeddingGenerator();
        var command = new EmbedCommand(database, generator, NullLogger<EmbedCommand>.Instance);
        return await command.EmbedAsync(vehicleDisplayName, CancellationToken.None);
    }

    /// <summary>
    /// Seeds a vehicle whose NHTSA identity is unique to this test. The container is shared by the
    /// whole collection and vehicles are unique on make, model and year, so reusing a real identity
    /// such as FORD EXPLORER 2013 collides with whichever other test seeded it first.
    /// </summary>
    private async Task<int> SeedVehicleWithChunksAsync(string displayName, string nhtsaModel, int modelYear, int chunkCount)
    {
        await using var context = postgres.CreateContext();
        var uniqueModel = $"{nhtsaModel}-EMBED-{modelYear}";
        var vehicle = Vehicle.Create("FORD", uniqueModel, modelYear, displayName);
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync(CancellationToken.None);

        var document = SourceDocument.Create(
            SourceKind.Complaint, $"embed-{displayName}", vehicle.Id, "STRUCTURE",
            new DateOnly(2020, 1, 1), "Complaint", "Exhaust odor enters the cabin.", "{}");
        context.SourceDocuments.Add(document);
        await context.SaveChangesAsync(CancellationToken.None);

        for (var ordinal = 0; ordinal < chunkCount; ordinal++)
        {
            context.DocumentChunks.Add(DocumentChunk.Create(document.Id, ordinal, $"{displayName} passage {ordinal}"));
        }

        await context.SaveChangesAsync(CancellationToken.None);
        return vehicle.Id;
    }
}
