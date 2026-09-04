// Runs dense, sparse and hybrid retrieval over one vehicle's records and explains every rank.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using RecallRadar.Domain.Retrieval;
using RecallRadar.Retrieval.Embeddings;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Retrieval.Search;

/// <summary>
/// The three retrieval paths, each returning an ordered list of chunk ids, and the fusion that
/// combines two of them.
/// </summary>
/// <remarks>
/// Drift justification (Article VII): <c>Microsoft.Extensions.VectorData</c> would abstract the
/// vector store, but it returns one score per hit and no keyword rank. FR-007 requires the dense
/// and sparse positions separately, so the two candidate queries are written here in SQL against
/// the operators PostgreSQL already provides: <c>&lt;=&gt;</c> for cosine distance and
/// <c>ts_rank_cd</c> over the generated tsvector.
/// </remarks>
public sealed class HybridSearchService(
    RecallRadarDbContext database,
    IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
{
    private const string FullTextLanguage = "english";

    /// <summary>Runs the request and returns hits in fused order, longest-explanation first.</summary>
    public async Task<IReadOnlyList<SearchHit>> SearchAsync(SearchRequest request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await GuardVehicleExistsAsync(request.VehicleId, cancellationToken);

        var explanations = request.Mode switch
        {
            RetrievalMode.Sparse => SingleMethod(
                await FindSparseCandidatesAsync(request, cancellationToken), RetrievalMode.Sparse),
            RetrievalMode.Dense => SingleMethod(
                await FindDenseCandidatesAsync(request, cancellationToken), RetrievalMode.Dense),
            _ => ReciprocalRankFusion.FuseDenseAndSparse(
                await FindDenseCandidatesAsync(request, cancellationToken),
                await FindSparseCandidatesAsync(request, cancellationToken)),
        };

        return await DescribeAsync(explanations.Take(request.Limit).ToList(), cancellationToken);
    }

    /// <summary>Turns one method's ordered ids into explanations carrying that method's rank only.</summary>
    private static List<(long ChunkId, RankExplanation Explanation)> SingleMethod(
        IReadOnlyList<long> order, RetrievalMode mode) =>
        [.. order.Select((chunkId, index) => (
            chunkId,
            RankExplanation.SingleMethod(mode, index + 1, 1d / (ReciprocalRankFusion.DefaultK + index + 1))))];

    private async Task GuardVehicleExistsAsync(int vehicleId, CancellationToken cancellationToken)
    {
        var isKnown = await database.Vehicles.AnyAsync(vehicle => vehicle.Id == vehicleId, cancellationToken);
        if (!isKnown)
        {
            throw new VehicleNotFoundException(vehicleId);
        }
    }

    /// <summary>
    /// Keyword candidates, ordered by how well the chunk covers the query terms. Works with no
    /// embedding key at all, which is why it is the mode available today.
    /// </summary>
    private async Task<IReadOnlyList<long>> FindSparseCandidatesAsync(
        SearchRequest request, CancellationToken cancellationToken)
    {
        // The question is turned into an OR of its terms. Joining with AND, which is what a bare
        // phrase does, means a whole question matches nothing at all.
        var query = KeywordQuery.BuildAnyTermQuery(request.Query);
        if (query is null)
        {
            return [];
        }

        return await ApplyFilters(database.DocumentChunks.AsNoTracking(), request)
            .Where(chunk => chunk.SearchText!.Matches(EF.Functions.ToTsQuery(FullTextLanguage, query)))
            .OrderByDescending(chunk => chunk.SearchText!.RankCoverDensity(EF.Functions.ToTsQuery(FullTextLanguage, query)))
            .ThenBy(chunk => chunk.Id)
            .Select(chunk => chunk.Id)
            .Take(SearchRequest.CandidateWindow)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Meaning candidates, ordered by cosine distance from the embedded query. Throws rather than
    /// returning nothing when no provider is configured or the vehicle was never embedded, so the
    /// caller reports why instead of showing an empty page.
    /// </summary>
    private async Task<IReadOnlyList<long>> FindDenseCandidatesAsync(
        SearchRequest request, CancellationToken cancellationToken)
    {
        var embeddedCount = await ApplyFilters(database.DocumentChunks.AsNoTracking(), request)
            .CountAsync(chunk => chunk.Embedding != null, cancellationToken);
        if (embeddedCount == 0)
        {
            throw new EmbeddingsUnavailableException(
                "No chunk for this vehicle has an embedding yet. Run the embed command, or search with mode=sparse.");
        }

        var queryVector = await EmbedQueryAsync(request.Query, cancellationToken);
        return await ApplyFilters(database.DocumentChunks.AsNoTracking(), request)
            .Where(chunk => chunk.Embedding != null)
            .OrderBy(chunk => chunk.Embedding!.CosineDistance(queryVector))
            .ThenBy(chunk => chunk.Id)
            .Select(chunk => chunk.Id)
            .Take(SearchRequest.CandidateWindow)
            .ToListAsync(cancellationToken);
    }

    private async Task<Vector> EmbedQueryAsync(string query, CancellationToken cancellationToken)
    {
        var generated = await embeddingGenerator.GenerateAsync([query], options: null, cancellationToken);
        return new Vector(generated[0].Vector);
    }

    /// <summary>Applies the structured filters. Vehicle scope is not optional: results never cross vehicles.</summary>
    private static IQueryable<DocumentChunk> ApplyFilters(IQueryable<DocumentChunk> chunks, SearchRequest request)
    {
        chunks = chunks.Where(chunk => chunk.Document!.VehicleId == request.VehicleId);

        if (request.Component is { } component)
        {
            chunks = chunks.Where(chunk => chunk.Document!.Component == component);
        }

        if (request.FiledFrom is { } from)
        {
            chunks = chunks.Where(chunk => chunk.Document!.FiledOn >= from);
        }

        if (request.FiledTo is { } to)
        {
            chunks = chunks.Where(chunk => chunk.Document!.FiledOn <= to);
        }

        return chunks;
    }

    /// <summary>Loads the records behind the ranked chunk ids, preserving the ranked order.</summary>
    private async Task<IReadOnlyList<SearchHit>> DescribeAsync(
        IReadOnlyList<(long ChunkId, RankExplanation Explanation)> ranked, CancellationToken cancellationToken)
    {
        if (ranked.Count == 0)
        {
            return [];
        }

        var chunkIds = ranked.Select(entry => entry.ChunkId).ToList();
        var rows = await database.DocumentChunks.AsNoTracking()
            .Where(chunk => chunkIds.Contains(chunk.Id))
            .Select(chunk => new
            {
                chunk.Id,
                chunk.Text,
                chunk.DocumentId,
                chunk.Document!.Kind,
                chunk.Document.ExternalId,
                chunk.Document.Title,
                chunk.Document.Component,
                chunk.Document.FiledOn,
            })
            .ToDictionaryAsync(row => row.Id, cancellationToken);

        return
        [
            .. ranked
                .Where(entry => rows.ContainsKey(entry.ChunkId))
                .Select(entry =>
                {
                    var row = rows[entry.ChunkId];
                    return new SearchHit(
                        row.DocumentId, row.Id, row.Kind, row.ExternalId, row.Title, row.Component,
                        row.FiledOn, SearchHit.BuildSnippet(row.Text), entry.Explanation);
                }),
        ];
    }
}

/// <summary>Thrown when a request names a vehicle that was never registered; the endpoint returns 404.</summary>
public sealed class VehicleNotFoundException(int vehicleId)
    : Exception($"No vehicle with id {vehicleId} is registered.")
{
    public int VehicleId { get; } = vehicleId;
}
