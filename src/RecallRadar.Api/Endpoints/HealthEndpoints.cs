// Reports what the server can actually do right now, so the client disables what it cannot.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecallRadar.Api.Answering;
using RecallRadar.Api.Config;
using RecallRadar.Api.Persistence;
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

    /// <summary>Reachable, but missing tables this build of the API needs.</summary>
    public const string SchemaBehind = "schema-outdated";

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
        var databaseState = await CheckDatabaseAsync(database, cancellationToken);
        var isDatabaseUsable = databaseState == Available;

        // Answering is reported from what is registered rather than from the key, so the answer
        // this gives always matches what the ask endpoint will actually do.
        var canAnswer = services.GetService<AnswerService>() is not null;
        var report = new HealthResponse(
            isDatabaseUsable ? Available : Unavailable,
            databaseState,
            settings.HasVoyageKey ? Available : Unavailable,
            canAnswer ? Available : Unavailable);

        return isDatabaseUsable
            ? Results.Ok(report)
            : Results.Json(report, statusCode: StatusCodes.Status503ServiceUnavailable);
    }

    /// <summary>
    /// A failed connection is a reportable state, not an exception to propagate.
    /// </summary>
    /// <remarks>
    /// This asks two questions, because a database can answer the first and fail the second: it can
    /// be reached, and it holds every table this build needs. Checking only reachability once let
    /// health report "ok" against a database missing the table behind the loads list, so the first
    /// sign of trouble was a 500 in somebody's browser.
    /// </remarks>
    private static async Task<string> CheckDatabaseAsync(RecallRadarDbContext database, CancellationToken cancellationToken)
    {
        try
        {
            if (!await database.Database.CanConnectAsync(cancellationToken))
            {
                return Unavailable;
            }

            var pending = await SchemaMigrator.FindPendingAsync(database, cancellationToken);
            return pending.Count == 0 ? Available : SchemaBehind;
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            return Unavailable;
        }
    }
}

/// <summary>The health shape from contracts/http-api.md.</summary>
public sealed record HealthResponse(string Status, string Database, string Embeddings, string Answering);
