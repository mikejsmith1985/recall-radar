// Registering a vehicle and watching its records load, so adding a car is not a command-line job.
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecallRadar.Api.Loading;
using RecallRadar.Domain.Vehicles;
using RecallRadar.Ingest.Config;
using RecallRadar.Ingest.Nhtsa;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Api.Endpoints;

/// <summary>
/// Maps POST /api/vehicles and the load-status endpoints.
/// </summary>
/// <remarks>
/// A load reaches three NHTSA feeds and takes minutes, so the request cannot wait for it. The
/// endpoint validates what it can immediately — the model name against NHTSA's own list, which is
/// the mistake people actually make — then queues the work and answers 202 with somewhere to look.
/// </remarks>
public static class VehicleEndpoints
{
    private const string InvalidRequestTitle = "Invalid vehicle";
    private const string UnknownModelTitle = "Unknown NHTSA model";
    private const string UnknownVehicleTitle = "Unknown vehicle";
    private const string FeedUnavailableTitle = "NHTSA unavailable";
    private const string UnknownLoadTitle = "Unknown load";
    private const string LoadUnfinishedTitle = "Load still running";

    /// <summary>
    /// How many known model names to suggest when one is rejected. Few, because they are ranked
    /// by resemblance now: twenty alphabetical names once stopped at "F-59" and hid the Mach-E.
    /// </summary>
    public const int SuggestionLimit = 8;

    /// <summary>Registers the vehicle write and load-status endpoints.</summary>
    public static IEndpointRouteBuilder MapVehicleEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapPost("/api/vehicles", RegisterAsync);
        endpoints.MapPost("/api/vehicles/{id:int}/refresh", RefreshAsync);
        endpoints.MapGet("/api/loads/{id:long}", ReadLoadAsync);
        endpoints.MapGet("/api/loads", ListLoadsAsync);
        endpoints.MapDelete("/api/loads/{id:long}", DismissLoadAsync);
        endpoints.MapGet("/api/nhtsa/models", ListNhtsaModelsAsync);
        return endpoints;
    }

    /// <summary>Validates the registration, then queues the load that fills it.</summary>
    private static async Task<IResult> RegisterAsync(
        RegisterVehicleRequest request,
        [FromServices] IngestJobQueue queue,
        [FromServices] NhtsaModelsClient models,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Results.Problem(
                detail: "A vehicle is required.", statusCode: StatusCodes.Status400BadRequest, title: InvalidRequestTitle);
        }

        if (!request.TryBuildRegistration(out var registration, out var problem))
        {
            return Results.Problem(
                detail: problem, statusCode: StatusCodes.Status400BadRequest, title: InvalidRequestTitle);
        }

        var rejection = await FindModelRejectionAsync(models, registration!, cancellationToken);
        if (rejection is not null)
        {
            return rejection;
        }

        // Asking twice joins the load already running rather than starting a second pass over the
        // same feeds. The caller gets the same job to watch either way.
        if (await queue.HasUnfinishedLoadAsync(registration!.DisplayName, cancellationToken)
            && await queue.FindLatestForDisplayNameAsync(registration.DisplayName, cancellationToken) is { } running)
        {
            return Results.Accepted($"/api/loads/{running.Id}", LoadResponse.From(running));
        }

        var job = await queue.QueueAsync(registration, IngestTrigger.Manual, cancellationToken);
        return Results.Accepted($"/api/loads/{job.Id}", LoadResponse.From(job));
    }

    /// <summary>Queues another pass over the feeds for a vehicle that already exists.</summary>
    private static async Task<IResult> RefreshAsync(
        int id,
        [FromServices] IngestJobQueue queue,
        [FromServices] RecallRadarDbContext database,
        CancellationToken cancellationToken)
    {
        var vehicle = await database.Vehicles.AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (vehicle is null)
        {
            return Results.Problem(
                detail: $"No vehicle with id {id} is registered.",
                statusCode: StatusCodes.Status404NotFound, title: UnknownVehicleTitle);
        }

        if (await queue.HasUnfinishedLoadAsync(vehicle.DisplayName, cancellationToken)
            && await queue.FindLatestForDisplayNameAsync(vehicle.DisplayName, cancellationToken) is { } running)
        {
            return Results.Accepted($"/api/loads/{running.Id}", LoadResponse.From(running));
        }

        var job = await queue.QueueAsync(
            ScheduledRefreshService.ToRegistration(vehicle), IngestTrigger.Manual, cancellationToken);
        return Results.Accepted($"/api/loads/{job.Id}", LoadResponse.From(job));
    }

    /// <summary>One load, so a caller can poll the one it started.</summary>
    private static async Task<IResult> ReadLoadAsync(
        long id, [FromServices] IngestJobQueue queue, CancellationToken cancellationToken)
    {
        var job = await queue.FindAsync(id, cancellationToken);
        return job is null ? Results.NotFound() : Results.Ok(LoadResponse.From(job));
    }

    /// <summary>Removes a finished load from the history.</summary>
    /// <remarks>
    /// The history exists so a refresh that failed overnight is visible rather than silent, which
    /// means it fills with rows nobody needs any more once they have been read. Dismissing one is
    /// how somebody says they have read it. A load still queued or running answers 409: the runner
    /// claims jobs by row, and removing one underneath it would leave work half done.
    /// </remarks>
    private static async Task<IResult> DismissLoadAsync(
        long id, [FromServices] IngestJobQueue queue, CancellationToken cancellationToken)
    {
        var outcome = await queue.DismissAsync(id, cancellationToken);
        return outcome switch
        {
            DismissOutcome.Dismissed => Results.NoContent(),
            DismissOutcome.StillRunning => Results.Problem(
                detail: $"Load {id} has not finished. A load can be dismissed once it has.",
                statusCode: StatusCodes.Status409Conflict, title: LoadUnfinishedTitle),
            _ => Results.Problem(
                detail: $"No load with id {id} exists.",
                statusCode: StatusCodes.Status404NotFound, title: UnknownLoadTitle),
        };
    }

    /// <summary>Recent loads, newest first, so the page can show what is happening without an id.</summary>
    private static async Task<IResult> ListLoadsAsync(
        [FromServices] RecallRadarDbContext database, CancellationToken cancellationToken)
    {
        var jobs = await database.IngestJobs.AsNoTracking()
            .OrderByDescending(job => job.QueuedAt).ThenByDescending(job => job.Id)
            .Take(LoadResponse.HistoryLimit)
            .ToListAsync(cancellationToken);
        return Results.Ok(jobs.Select(LoadResponse.From).ToList());
    }

    /// <summary>
    /// The model names NHTSA files complaints under for one make and year, so the page can offer
    /// them rather than make somebody guess a vocabulary that calls a Mach-E "MUSTANG MACH-E BEV BEV".
    /// </summary>
    private static async Task<IResult> ListNhtsaModelsAsync(
        [FromQuery] string? make,
        [FromQuery] int? modelYear,
        [FromServices] NhtsaModelsClient models,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(make) || modelYear is not { } year)
        {
            return Results.Problem(
                detail: "A make and a model year are both required.",
                statusCode: StatusCodes.Status400BadRequest, title: InvalidRequestTitle);
        }

        try
        {
            var known = await models.GetModelNamesAsync(make.Trim().ToUpperInvariant(), year, cancellationToken);
            // Distinct and ordered: NHTSA repeats a name once per body style, which reads as a bug.
            return Results.Ok(known.Distinct(StringComparer.OrdinalIgnoreCase).Order(StringComparer.Ordinal).ToList());
        }
        catch (HttpRequestException unreachable)
        {
            return Results.Problem(
                detail: $"NHTSA's model list could not be reached: {unreachable.Message}",
                statusCode: StatusCodes.Status503ServiceUnavailable, title: FeedUnavailableTitle);
        }
    }

    /// <summary>
    /// Rejects a model name NHTSA does not know, suggesting the ones it does. This is the error
    /// people actually hit, and the suggestions are what turn it into something they can act on.
    /// </summary>
    private static async Task<IResult?> FindModelRejectionAsync(
        NhtsaModelsClient models, VehicleRegistration registration, CancellationToken cancellationToken)
    {
        IReadOnlyCollection<string> known;
        try
        {
            known = await models.GetModelNamesAsync(registration.Make, registration.ModelYear, cancellationToken);
        }
        catch (HttpRequestException unreachable)
        {
            // The feed being down is not the caller's mistake, so it is not a 400.
            return Results.Problem(
                detail: $"NHTSA's model list could not be reached: {unreachable.Message}",
                statusCode: StatusCodes.Status503ServiceUnavailable, title: FeedUnavailableTitle);
        }

        if (known.Contains(registration.NhtsaModel))
        {
            return null;
        }

        // Ranked by resemblance rather than listed alphabetically, so the name being reached for
        // is first instead of cut off partway down the alphabet.
        var suggestions = ModelNameMatcher.Rank(registration.NhtsaModel, known, SuggestionLimit);
        var remaining = known.Distinct(StringComparer.OrdinalIgnoreCase).Count() - suggestions.Count;
        var more = remaining > 0 ? $" ({remaining} more exist.)" : string.Empty;

        return Results.Problem(
            detail:
                $"NHTSA has no complaint model named '{registration.NhtsaModel}' for {registration.Make} " +
                $"{registration.ModelYear}. Did you mean: {string.Join(", ", suggestions)}?{more}",
            statusCode: StatusCodes.Status400BadRequest,
            title: UnknownModelTitle);
    }
}

/// <summary>A vehicle as the owner describes it, with the NHTSA names its records are filed under.</summary>
public sealed record RegisterVehicleRequest(
    string? Make, string? NhtsaModel, string? RecallModel, int ModelYear, string? DisplayName)
{
    /// <summary>Builds the registration, or returns the first thing wrong with the request.</summary>
    public bool TryBuildRegistration(out VehicleRegistration? registration, out string? problem)
    {
        registration = null;
        problem = FindProblem();
        if (problem is not null)
        {
            return false;
        }

        registration = new VehicleRegistration
        {
            Make = Make!.Trim().ToUpperInvariant(),
            NhtsaModel = NhtsaModel!.Trim().ToUpperInvariant(),
            RecallModel = string.IsNullOrWhiteSpace(RecallModel) ? null : RecallModel.Trim().ToUpperInvariant(),
            ModelYear = ModelYear,
            DisplayName = DisplayName!.Trim(),
        };
        return true;
    }

    private string? FindProblem()
    {
        if (string.IsNullOrWhiteSpace(Make))
        {
            return "A make is required, for example \"Ford\".";
        }

        if (string.IsNullOrWhiteSpace(NhtsaModel))
        {
            return "An NHTSA model name is required, for example \"EXPLORER\".";
        }

        if (string.IsNullOrWhiteSpace(DisplayName))
        {
            return "A display name is required, for example \"2013 Explorer Sport\".";
        }

        if (ModelYear < Vehicle.MinimumModelYear || ModelYear > Vehicle.MaximumModelYear)
        {
            return $"The model year must be between {Vehicle.MinimumModelYear} and {Vehicle.MaximumModelYear}.";
        }

        return null;
    }
}

/// <summary>One load as the client renders it: where it has got to, and what it produced.</summary>
public sealed record LoadResponse(
    long Id,
    int? VehicleId,
    string DisplayName,
    string State,
    string Trigger,
    bool IsFinished,
    DateTimeOffset QueuedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    string? Message,
    JsonElement? Report)
{
    /// <summary>How many recent loads the list returns. Enough to see what happened, not a log.</summary>
    public const int HistoryLimit = 20;

    /// <summary>Flattens a job for the wire.</summary>
    public static LoadResponse From(IngestJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return new LoadResponse(
            job.Id,
            job.VehicleId,
            job.DisplayName,
            job.State.ToString().ToLowerInvariant(),
            job.Trigger.ToString().ToLowerInvariant(),
            job.IsFinished,
            job.QueuedAt,
            job.StartedAt,
            job.FinishedAt,
            job.Message,
            ReadReport(job.ReportJson));
    }

    /// <summary>
    /// The counts as JSON, or null when the load has not produced any. A report written by an older
    /// version keeps whatever shape it had rather than being forced into today's fields.
    /// </summary>
    private static JsonElement? ReadReport(string reportJson)
    {
        if (string.IsNullOrWhiteSpace(reportJson))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(reportJson);
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
