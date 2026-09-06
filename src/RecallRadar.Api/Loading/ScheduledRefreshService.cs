// Queues a refresh for each vehicle on a schedule, so the corpus keeps up with NHTSA.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RecallRadar.Ingest.Config;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Api.Loading;

/// <summary>When to refresh, and whether to at all.</summary>
public sealed class ScheduledRefreshOptions
{
    public const string SectionName = "ScheduledRefresh";

    /// <summary>Off by default: a background process that reaches the internet should be opted into.</summary>
    public bool IsEnabled { get; init; }

    /// <summary>How stale a vehicle's last successful load may get before it is refreshed.</summary>
    public TimeSpan Interval { get; init; } = TimeSpan.FromHours(24);

    /// <summary>How often to check whether anything has gone stale.</summary>
    public TimeSpan CheckInterval { get; init; } = TimeSpan.FromMinutes(30);
}

/// <summary>
/// Queues a refresh for any vehicle whose records have gone stale.
/// </summary>
/// <remarks>
/// It queues work rather than doing it, so refreshes go through the same runner, the same one-at-a-
/// time discipline and the same job rows as a load someone asked for. Ingestion only inserts records
/// it does not already hold, so a refresh costs one pass over the feeds and adds whatever is new.
/// Staleness is measured from the last <em>successful</em> load: a failing vehicle is retried on the
/// same schedule rather than hammered.
/// </remarks>
public sealed class ScheduledRefreshService(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    IOptions<ScheduledRefreshOptions> options,
    ILogger<ScheduledRefreshService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.IsEnabled)
        {
            logger.LogInformation("Scheduled refresh is off. Loads happen only when asked for.");
            return;
        }

        logger.LogInformation(
            "Scheduled refresh on: vehicles older than {Interval}, checked every {Check}.",
            options.Value.Interval, options.Value.CheckInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await QueueStaleVehiclesAsync(stoppingToken);
            }
            catch (Exception failure) when (failure is not OperationCanceledException)
            {
                logger.LogWarning(failure, "Could not queue refreshes; will try again next check.");
            }

            try
            {
                await Task.Delay(options.Value.CheckInterval, clock, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    /// <summary>
    /// Queues one refresh per stale vehicle, skipping any that already has a load waiting, and
    /// returns how many it queued. Public so one pass can be driven directly: a test that waited a
    /// fixed time instead would pass or fail on how many vehicles happened to be in the database.
    /// </summary>
    public async Task<int> QueueStaleVehiclesAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.IsEnabled)
        {
            return 0;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RecallRadarDbContext>();
        var queue = scope.ServiceProvider.GetRequiredService<IngestJobQueue>();

        var vehicles = await database.Vehicles.AsNoTracking().ToListAsync(stoppingToken);
        var queued = 0;
        foreach (var vehicle in vehicles)
        {
            if (!await IsStaleAsync(database, queue, vehicle, stoppingToken))
            {
                continue;
            }

            await queue.QueueAsync(ToRegistration(vehicle), IngestTrigger.Scheduled, stoppingToken);
            queued++;
            logger.LogInformation("Queued a scheduled refresh for {Vehicle}.", vehicle.DisplayName);
        }

        return queued;
    }

    private async Task<bool> IsStaleAsync(
        RecallRadarDbContext database, IngestJobQueue queue, Vehicle vehicle, CancellationToken stoppingToken)
    {
        if (await queue.HasUnfinishedLoadAsync(vehicle.DisplayName, stoppingToken))
        {
            return false;
        }

        var lastSuccess = await database.IngestJobs.AsNoTracking()
            .Where(job => job.VehicleId == vehicle.Id && job.State == IngestJobState.Succeeded)
            .OrderByDescending(job => job.FinishedAt)
            .Select(job => job.FinishedAt)
            .FirstOrDefaultAsync(stoppingToken);

        // A vehicle loaded by the command line has no job row at all, so it counts as stale and
        // gets one. That is what puts a hand-loaded vehicle onto the schedule.
        return lastSuccess is null || clock.GetUtcNow() - lastSuccess.Value >= options.Value.Interval;
    }

    /// <summary>The registration to repeat, taken from the vehicle rather than a configuration file.</summary>
    public static VehicleRegistration ToRegistration(Vehicle vehicle)
    {
        ArgumentNullException.ThrowIfNull(vehicle);
        return new VehicleRegistration
        {
            Make = vehicle.Make,
            NhtsaModel = vehicle.NhtsaModel,
            RecallModel = vehicle.RecallModel,
            ModelYear = vehicle.ModelYear,
            DisplayName = vehicle.DisplayName,
        };
    }
}
