// Runs queued loads one at a time in the background, so a request never waits on three NHTSA feeds.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RecallRadar.Ingest;
using RecallRadar.Ingest.Commands;
using RecallRadar.Ingest.Config;
using RecallRadar.Retrieval.Embeddings;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Api.Loading;

/// <summary>How often the runner looks for work, and how it behaves when a load fails.</summary>
public sealed class IngestRunnerOptions
{
    public const string SectionName = "IngestRunner";

    /// <summary>
    /// Whether this process runs queued loads. On by default. A test that hosts the API only to
    /// exercise an endpoint turns it off, so its runner cannot claim a job another test is watching
    /// and load it against a stub that test never configured.
    /// </summary>
    public bool IsEnabled { get; init; } = true;

    /// <summary>How long to wait before looking again when nothing is queued.</summary>
    public TimeSpan PollInterval { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Whether a successful load also back-fills embeddings. Off leaves keyword search working.</summary>
    public bool EmbedAfterLoad { get; init; } = true;
}

/// <summary>
/// Takes one queued load at a time and runs it to completion.
/// </summary>
/// <remarks>
/// Serial by design. A load fetches every complaint for a vehicle, every recall, and a four-megabyte
/// flat file, all from feeds that belong to somebody else. Running two at once would double that
/// traffic for no gain, since the work is waiting on the network rather than on this machine.
/// A failure is recorded on the job and the loop continues: one bad registration must not stop
/// every later load.
/// </remarks>
public sealed class IngestJobRunner(
    IServiceScopeFactory scopeFactory,
    TimeProvider clock,
    IOptions<IngestRunnerOptions> options,
    ILogger<IngestJobRunner> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions ReportJsonOptions = new(JsonSerializerDefaults.Web);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!options.Value.IsEnabled)
        {
            logger.LogInformation("Ingest runner is off; queued loads will wait for a process that runs them.");
            return;
        }

        logger.LogInformation("Ingest runner started; polling every {Interval}.", options.Value.PollInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            var didWork = await TryRunOneAsync(stoppingToken);
            if (!didWork)
            {
                await SafeDelayAsync(stoppingToken);
            }
        }
    }

    /// <summary>Runs one job if any is waiting. Returns whether there was work, so the loop can pace itself.</summary>
    private async Task<bool> TryRunOneAsync(CancellationToken stoppingToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var queue = scope.ServiceProvider.GetRequiredService<IngestJobQueue>();

        IngestJob? job;
        try
        {
            job = await queue.ClaimNextAsync(stoppingToken);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            // The database is unreachable or the claim raced. Neither is worth ending the runner over.
            logger.LogWarning(failure, "Could not claim a load; will look again shortly.");
            return false;
        }

        if (job is null)
        {
            return false;
        }

        await RunAsync(scope.ServiceProvider, job, stoppingToken);
        return true;
    }

    /// <summary>Loads the vehicle, embeds what it added, and records the outcome on the job either way.</summary>
    private async Task RunAsync(IServiceProvider services, IngestJob job, CancellationToken stoppingToken)
    {
        var database = services.GetRequiredService<RecallRadarDbContext>();
        try
        {
            var report = await services.GetRequiredService<IngestService>()
                .IngestAsync(ToRegistration(job), stoppingToken);
            var vehicleId = await FindVehicleIdAsync(services, job, stoppingToken);
            var embedded = await TryEmbedAsync(services, job, stoppingToken);

            job.Succeed(vehicleId, Describe(report, embedded), clock.GetUtcNow());
            await database.SaveChangesAsync(stoppingToken);
            logger.LogInformation(
                "Loaded {Vehicle}: {Complaints} complaints, {Recalls} recalls, {Investigations} investigations.",
                job.DisplayName, report.ComplaintsNew, report.RecallsNew, report.InvestigationsNew);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception failure)
        {
            // Recorded rather than rethrown: a job that vanished is indistinguishable from one
            // still running, and the person who asked for it deserves the reason.
            job.Fail(LoadFailureMessage.Describe(failure), clock.GetUtcNow());
            await database.SaveChangesAsync(CancellationToken.None);
            logger.LogWarning(failure, "Load of {Vehicle} failed.", job.DisplayName);
        }
    }

    /// <summary>
    /// Back-fills embeddings for what the load added. A failure here is not a failed load: keyword
    /// search works without vectors, so the records are still worth having.
    /// </summary>
    private async Task<int> TryEmbedAsync(IServiceProvider services, IngestJob job, CancellationToken stoppingToken)
    {
        if (!options.Value.EmbedAfterLoad)
        {
            return 0;
        }

        try
        {
            var report = await services.GetRequiredService<EmbedCommand>()
                .EmbedAsync(job.DisplayName, stoppingToken);
            return report.Embedded;
        }
        catch (Exception failure) when (failure is EmbeddingsUnavailableException or EmbeddingProviderUnavailableException)
        {
            logger.LogInformation("Loaded {Vehicle} without embeddings: {Reason}", job.DisplayName, failure.Message);
            return 0;
        }
    }

    private static async Task<int> FindVehicleIdAsync(
        IServiceProvider services, IngestJob job, CancellationToken stoppingToken)
    {
        var database = services.GetRequiredService<RecallRadarDbContext>();
        var vehicle = await database.Vehicles.AsNoTracking().FirstOrDefaultAsync(
            candidate => candidate.Make == job.Make
                && candidate.NhtsaModel == job.NhtsaModel
                && candidate.ModelYear == job.ModelYear,
            stoppingToken);
        return vehicle?.Id ?? 0;
    }

    /// <summary>The registration the job recorded, so a load repeats exactly what was asked for.</summary>
    public static VehicleRegistration ToRegistration(IngestJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return new VehicleRegistration
        {
            Make = job.Make,
            NhtsaModel = job.NhtsaModel,
            RecallModel = job.RecallModel,
            ModelYear = job.ModelYear,
            DisplayName = job.DisplayName,
        };
    }

    /// <summary>The counts, in the shape the load endpoint returns them.</summary>
    public static string Describe(IngestReport report, int chunksEmbedded)
    {
        ArgumentNullException.ThrowIfNull(report);
        return JsonSerializer.Serialize(
            new
            {
                complaintsNew = report.ComplaintsNew,
                complaintsUnchanged = report.ComplaintsUnchanged,
                recallsNew = report.RecallsNew,
                investigationsNew = report.InvestigationsNew,
                linksCreated = report.LinksCreated,
                chunksCreated = report.ChunksCreated,
                chunksEmbedded,
            },
            ReportJsonOptions);
    }

    private async Task SafeDelayAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(options.Value.PollInterval, clock, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Shutting down. The while loop's own check ends the runner.
        }
    }
}
