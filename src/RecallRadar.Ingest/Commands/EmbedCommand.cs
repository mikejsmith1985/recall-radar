// Gives vectors to the chunks stored before an embedding provider existed.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Pgvector;
using RecallRadar.Retrieval.Embeddings;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Ingest.Commands;

/// <summary>
/// Back-fills <c>document_chunks.embedding</c> where it is null. Ingest deliberately stores chunks
/// without vectors when no provider is configured, so keyword search works from the first load;
/// this is the pass that makes dense and hybrid search possible afterwards.
/// </summary>
/// <remarks>
/// Each batch is saved before the next is requested. A run that fails part-way therefore keeps the
/// vectors it already earned, and running the verb again simply resumes: the query selects on
/// <c>embedding IS NULL</c>, so finished chunks are never re-sent to a paid API.
/// </remarks>
public sealed class EmbedCommand(
    RecallRadarDbContext database,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
    ILogger<EmbedCommand> logger)
{
    /// <summary>Voyage's per-request input limit, and therefore the save interval.</summary>
    public const int BatchSize = VoyageEmbeddingGenerator.MaxBatchSize;

    /// <summary>
    /// Embeds every chunk that has no vector, optionally limited to one vehicle.
    /// </summary>
    /// <exception cref="EmbeddingsUnavailableException">No embedding provider is configured.</exception>
    public async Task<EmbedReport> EmbedAsync(string? vehicleDisplayName, CancellationToken cancellationToken)
    {
        EnsureProviderIsAvailable();
        var embedded = 0;

        while (await LoadNextBatchAsync(vehicleDisplayName, cancellationToken) is { Count: > 0 } batch)
        {
            await EmbedBatchAsync(batch, cancellationToken);
            embedded += batch.Count;
            logger.LogInformation("Embedded {Embedded} chunks so far.", embedded);
        }

        return new EmbedReport
        {
            Embedded = embedded,
            Remaining = await CountPendingAsync(vehicleDisplayName, cancellationToken),
        };
    }

    /// <summary>
    /// Asks the container whether embeddings are possible before reading a single row, so a missing
    /// key fails immediately with the wording the contract specifies rather than after a query.
    /// </summary>
    private void EnsureProviderIsAvailable()
    {
        if (embeddingGenerator.GetService(typeof(NullEmbeddingGenerator)) is not null)
        {
            throw EmbeddingsUnavailableException.NoProviderConfigured();
        }
    }

    private Task<List<DocumentChunk>> LoadNextBatchAsync(string? vehicleDisplayName, CancellationToken cancellationToken) =>
        FilterPending(vehicleDisplayName).OrderBy(chunk => chunk.Id).Take(BatchSize).ToListAsync(cancellationToken);

    private Task<int> CountPendingAsync(string? vehicleDisplayName, CancellationToken cancellationToken) =>
        FilterPending(vehicleDisplayName).CountAsync(cancellationToken);

    /// <summary>Chunks with no vector, narrowed to one vehicle when the caller named one.</summary>
    private IQueryable<DocumentChunk> FilterPending(string? vehicleDisplayName)
    {
        var pending = database.DocumentChunks.Where(chunk => chunk.Embedding == null);
        if (string.IsNullOrWhiteSpace(vehicleDisplayName))
        {
            return pending;
        }

        var displayName = vehicleDisplayName.Trim();
        return pending.Where(chunk => chunk.Document!.Vehicle!.DisplayName == displayName);
    }

    private async Task EmbedBatchAsync(IReadOnlyList<DocumentChunk> batch, CancellationToken cancellationToken)
    {
        var embeddings = await embeddingGenerator.GenerateAsync(
            batch.Select(chunk => chunk.Text), VoyageEmbeddingGenerator.ForDocument(), cancellationToken);

        if (embeddings.Count != batch.Count)
        {
            throw new InvalidOperationException(
                $"Asked for {batch.Count} embeddings and received {embeddings.Count}; storing them would misalign vectors and text.");
        }

        for (var index = 0; index < batch.Count; index++)
        {
            batch[index].SetEmbedding(new Vector(embeddings[index].Vector));
        }

        await database.SaveChangesAsync(cancellationToken);
    }
}
