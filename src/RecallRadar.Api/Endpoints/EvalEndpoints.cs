// Exposes what the evaluation measured, so the numbers are visible rather than claimed.
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Api.Endpoints;

/// <summary>
/// Maps GET /api/eval. Running the evaluation is a command-line concern, because it is slow and
/// deliberate; reading the result is a page anyone can open.
/// </summary>
public static class EvalEndpoints
{
    /// <summary>How many past runs the history carries. Enough to see a trend, not a log.</summary>
    public const int HistoryLimit = 20;

    /// <summary>Registers the evaluation endpoint.</summary>
    public static IEndpointRouteBuilder MapEvalEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapGet("/api/eval", ReadEvaluationsAsync);
        return endpoints;
    }

    private static async Task<IResult> ReadEvaluationsAsync(
        [FromServices] RecallRadarDbContext database, CancellationToken cancellationToken)
    {
        var runs = await database.EvaluationRuns.AsNoTracking()
            .OrderByDescending(run => run.RanAt).ThenByDescending(run => run.Id)
            .Take(HistoryLimit)
            .Select(run => new { run.Id, run.RanAt, run.CaseCount, run.MetricsJson, run.VehicleId })
            .ToListAsync(cancellationToken);

        var described = runs.Select(run => new EvaluationRunResponse(
            run.Id, run.RanAt, run.CaseCount, run.VehicleId, ReadMetrics(run.MetricsJson))).ToList();

        // Null rather than an empty object before the first run: the client shows "not measured
        // yet", which is honest, instead of a table of zeroes that reads as a result.
        return Results.Ok(new EvaluationsResponse(
            described.FirstOrDefault(),
            [.. described.Skip(1)]));
    }

    /// <summary>
    /// Reads stored metrics back as JSON. A run written by an older version keeps whatever shape
    /// it had, so this never assumes today's fields are present.
    /// </summary>
    private static JsonElement ReadMetrics(string metricsJson)
    {
        try
        {
            using var document = JsonDocument.Parse(metricsJson);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return JsonDocument.Parse("{}").RootElement.Clone();
        }
    }
}

/// <summary>One recorded evaluation run.</summary>
public sealed record EvaluationRunResponse(
    long Id, DateTimeOffset RanAt, int CaseCount, int? VehicleId, JsonElement Metrics);

/// <summary>The latest run and the runs behind it.</summary>
public sealed record EvaluationsResponse(EvaluationRunResponse? Latest, IReadOnlyList<EvaluationRunResponse> History);
