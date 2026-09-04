// Reports what the server can actually do right now, so the client disables what it cannot.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecallRadar.Api.Answering;
using RecallRadar.Api.Config;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Api.Endpoints;

/// <summary>
/// Health as capability, not just liveness. A missing embedding or answering key is a normal
/// operating state here, not a fault: keyword search still works, so the response says which
/// features are available and the client greys out the rest.
/// </summary>
public static class HealthEndpoints
{
    public const string Available = "ok";
    public const string Unavailable = "unavailable";

    /// <summary>Registers GET /health.</summary>
    public static IEndpointRouteBuilder MapHealthEndpoint(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet("/health", ReportHealthAsync);
        return endpoints;
    }

    private static async Task<IResult> ReportHealthAsync(
        RecallRadarDbContext database, AppSettings settings, IServiceProvider services, CancellationToken cancellationToken)
    {
        var isDatabaseReachable = await CanReachDatabaseAsync(database, cancellationToken);

        // Answering is reported from what is registered rather than from the key, so the answer
        // this gives always matches what the ask endpoint will actually do.
        var canAnswer = services.GetService<AnswerService>() is not null;
        var report = new HealthResponse(
            isDatabaseReachable ? Available : Unavailable,
            isDatabaseReachable ? Available : Unavailable,
            settings.HasVoyageKey ? Available : Unavailable,
            canAnswer ? Available : Unavailable);

        return isDatabaseReachable
            ? Results.Ok(report)
            : Results.Json(report, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    /// <summary>A failed connection is a reportable state, not an exception to propagate.</summary>
    private static async Task<bool> CanReachDatabaseAsync(RecallRadarDbContext database, CancellationToken cancellationToken)
    {
        try
        {
            return await database.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            return false;
        }
    }
}

/// <summary>The health shape from contracts/http-api.md.</summary>
public sealed record HealthResponse(string Status, string Database, string Embeddings, string Answering);
