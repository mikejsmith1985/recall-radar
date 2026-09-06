// The read endpoints: which vehicles exist, and what a query retrieves from one of them.
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecallRadar.Retrieval.Embeddings;
using RecallRadar.Retrieval.Persistence;
using RecallRadar.Retrieval.Search;

namespace RecallRadar.Api.Endpoints;

/// <summary>
/// Maps the vehicle and search endpoints described in contracts/http-api.md. Each hit carries the
/// ranks that produced it, because showing why a record was retrieved is part of the product.
/// </summary>
public static class SearchEndpoints
{
    private const string EmbeddingsUnavailableTitle = "Embeddings are unavailable";
    private const string UnknownVehicleTitle = "Unknown vehicle";
    private const string InvalidRequestTitle = "Invalid search request";
    private const string EmbeddingProviderDownTitle = "Embedding provider unavailable";

    /// <summary>Registers GET /api/vehicles and GET /api/search.</summary>
    public static IEndpointRouteBuilder MapSearchEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet("/api/vehicles", ListVehiclesAsync);
        endpoints.MapGet("/api/search", SearchAsync);
        return endpoints;
    }

    /// <summary>Every registered vehicle with how many records of each kind it holds.</summary>
    private static async Task<IResult> ListVehiclesAsync(
        RecallRadarDbContext database, CancellationToken cancellationToken)
    {
        var vehicles = await database.Vehicles.AsNoTracking()
            .OrderBy(vehicle => vehicle.ModelYear).ThenBy(vehicle => vehicle.NhtsaModel)
            .Select(vehicle => new
            {
                vehicle.Id,
                vehicle.DisplayName,
                vehicle.Make,
                vehicle.ModelYear,
                Counts = database.SourceDocuments
                    .Where(document => document.VehicleId == vehicle.Id)
                    .GroupBy(document => document.Kind)
                    .Select(group => new { Kind = group.Key, Count = group.Count() })
                    .ToList(),
            })
            .ToListAsync(cancellationToken);

        return Results.Ok(vehicles.Select(vehicle => new VehicleResponse(
            vehicle.Id,
            vehicle.DisplayName,
            vehicle.Make,
            vehicle.ModelYear,
            new RecordCounts(
                CountOf(vehicle.Counts.ToDictionary(entry => entry.Kind, entry => entry.Count), SourceKind.Complaint),
                CountOf(vehicle.Counts.ToDictionary(entry => entry.Kind, entry => entry.Count), SourceKind.Recall),
                CountOf(vehicle.Counts.ToDictionary(entry => entry.Kind, entry => entry.Count), SourceKind.Investigation)))));
    }

    private static int CountOf(IReadOnlyDictionary<SourceKind, int> counts, SourceKind kind) =>
        counts.TryGetValue(kind, out var count) ? count : 0;

    /// <summary>Runs one search. Validation problems, unknown vehicles and a missing embedding provider each get their own status.</summary>
    private static async Task<IResult> SearchAsync(
        HybridSearchService search,
        [FromQuery] int vehicleId,
        [FromQuery] string? q,
        [FromQuery] string? mode,
        [FromQuery] string? component,
        [FromQuery] DateOnly? filedFrom,
        [FromQuery] DateOnly? filedTo,
        [FromQuery] int? limit,
        [FromQuery] string? scope,
        CancellationToken cancellationToken)
    {
        if (!SearchRequest.TryCreate(
            vehicleId, q, mode, component, filedFrom, filedTo, limit, scope, out var request, out var problem))
        {
            return Results.Problem(detail: problem, statusCode: StatusCodes.Status400BadRequest, title: InvalidRequestTitle);
        }

        try
        {
            var hits = await search.SearchAsync(request!, cancellationToken);
            return Results.Ok(new SearchResponse(
                request!.Mode.ToString().ToLowerInvariant(),
                [.. hits.Select(HitResponse.From)],
                request.Scope.ToString().ToLowerInvariant()));
        }
        catch (VehicleNotFoundException notFound)
        {
            return Results.Problem(detail: notFound.Message, statusCode: StatusCodes.Status404NotFound, title: UnknownVehicleTitle);
        }
        catch (EmbeddingProviderUnavailableException outage)
        {
            // The provider is configured but not answering. Temporary, so 503 rather than 409.
            return Results.Problem(
                detail: outage.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: EmbeddingProviderDownTitle);
        }
        catch (EmbeddingsUnavailableException unavailable)
        {
            // Not an error in the server: the operator has not supplied an embedding key yet, and
            // keyword search still works. The client uses this to disable the modes that need one.
            // The exception already names the remedy, so it is passed through rather than restated.
            return Results.Problem(
                detail: unavailable.Message,
                statusCode: StatusCodes.Status409Conflict,
                title: EmbeddingsUnavailableTitle);
        }
    }
}

/// <summary>How many records of each kind a vehicle holds, for the picker.</summary>
public sealed record RecordCounts(int Complaint, int Recall, int Investigation);

/// <summary>A vehicle as the client lists it.</summary>
public sealed record VehicleResponse(int Id, string DisplayName, string Make, int ModelYear, RecordCounts Counts);

/// <summary>One hit, with the ranks that explain its position.</summary>
public sealed record HitResponse(
    long DocumentId,
    long ChunkId,
    string Kind,
    string ExternalId,
    string Title,
    string Component,
    DateOnly? FiledOn,
    string Snippet,
    int? DenseRank,
    int? SparseRank,
    double FusedScore)
{
    /// <summary>Number of decimal places kept in the fused score; more would suggest false precision.</summary>
    public const int ScoreDecimals = 5;

    /// <summary>Flattens a hit into the wire shape, rounding the score to something a reader can compare.</summary>
    public static HitResponse From(SearchHit hit)
    {
        ArgumentNullException.ThrowIfNull(hit);
        return new HitResponse(
            hit.DocumentId,
            hit.ChunkId,
            hit.Kind.ToString().ToLowerInvariant(),
            hit.ExternalId,
            hit.Title,
            hit.Component,
            hit.FiledOn,
            hit.Snippet,
            hit.Explanation.DenseRank,
            hit.Explanation.SparseRank,
            Math.Round(hit.Explanation.FusedScore, ScoreDecimals));
    }
}

/// <summary>The search response: the mode that actually ran, and the hits.</summary>
/// <summary>One page of hits, naming the mode and the pool they were ranked within.</summary>
public sealed record SearchResponse(string Mode, IReadOnlyList<HitResponse> Hits, string Scope);
