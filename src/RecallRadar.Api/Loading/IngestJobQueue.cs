// Queues loads, hands them to the runner one at a time, and reports what happened to each.
using Microsoft.EntityFrameworkCore;
using RecallRadar.Ingest.Config;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Api.Loading;

/// <summary>
/// The waiting list of loads. Reads and writes <see cref="IngestJob"/> rows so a load survives a
/// restart: the request that asked for it is long gone by the time it finishes.
/// </summary>
public sealed class IngestJobQueue(RecallRadarDbContext database, TimeProvider clock)
{
    /// <summary>
    /// Claims the oldest waiting job for this runner. The row is locked and skipped if another
    /// runner already holds it, so two processes can never load the same vehicle twice.
    /// </summary>
    private const string ClaimNextSql =
        """
        SELECT * FROM ingest_jobs
        WHERE "State" = 1
        ORDER BY "QueuedAt"
        LIMIT 1
        FOR UPDATE SKIP LOCKED
        """;

    /// <summary>Adds a load to the waiting list and returns it.</summary>
    public async Task<IngestJob> QueueAsync(
        VehicleRegistration registration, IngestTrigger trigger, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);
        registration.Validate();

        var job = IngestJob.Queue(
            registration.Make, registration.NhtsaModel, registration.RecallModel,
            registration.ModelYear, registration.DisplayName, trigger, clock.GetUtcNow());
        database.IngestJobs.Add(job);
        await database.SaveChangesAsync(cancellationToken);
        return job;
    }

    /// <summary>
    /// Takes the next waiting job and marks it running, inside one transaction, or returns null
    /// when nothing is waiting. The caller must dispose the transaction it is handed.
    /// </summary>
    public async Task<IngestJob?> ClaimNextAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var job = await database.IngestJobs.FromSqlRaw(ClaimNextSql).FirstOrDefaultAsync(cancellationToken);
        if (job is null)
        {
            await transaction.RollbackAsync(cancellationToken);
            return null;
        }

        job.Start(clock.GetUtcNow());
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return job;
    }

    /// <summary>The most recent load for one vehicle, by the name it was registered under.</summary>
    public Task<IngestJob?> FindLatestForDisplayNameAsync(string displayName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        var trimmed = displayName.Trim();
        return database.IngestJobs.AsNoTracking()
            .Where(job => job.DisplayName == trimmed)
            .OrderByDescending(job => job.QueuedAt).ThenByDescending(job => job.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    /// <summary>One load by its id, for a caller polling the one it started.</summary>
    public Task<IngestJob?> FindAsync(long jobId, CancellationToken cancellationToken) =>
        database.IngestJobs.AsNoTracking().FirstOrDefaultAsync(job => job.Id == jobId, cancellationToken);

    /// <summary>
    /// Removes a finished load from the history, and reports what it did.
    /// </summary>
    /// <remarks>
    /// A load that is queued or running is refused rather than deleted: the runner claims jobs by
    /// row, so removing one underneath it would leave work half done with nothing recording it. The
    /// row is a log entry, not a record of the vehicle, so a finished one is deleted outright rather
    /// than hidden behind a flag that would make the list disagree with the table.
    /// </remarks>
    public async Task<DismissOutcome> DismissAsync(long jobId, CancellationToken cancellationToken)
    {
        var job = await database.IngestJobs.FirstOrDefaultAsync(candidate => candidate.Id == jobId, cancellationToken);
        if (job is null)
        {
            return DismissOutcome.NotFound;
        }

        if (!job.IsFinished)
        {
            return DismissOutcome.StillRunning;
        }

        database.IngestJobs.Remove(job);
        await database.SaveChangesAsync(cancellationToken);
        return DismissOutcome.Dismissed;
    }

    /// <summary>
    /// True when this vehicle already has a load queued or running. Asking twice for the same
    /// vehicle should join the load in progress, not start a second one against the same feeds.
    /// </summary>
    public Task<bool> HasUnfinishedLoadAsync(string displayName, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        var trimmed = displayName.Trim();
        return database.IngestJobs.AsNoTracking().AnyAsync(
            job => job.DisplayName == trimmed
                && (job.State == IngestJobState.Queued || job.State == IngestJobState.Running),
            cancellationToken);
    }
}

/// <summary>What happened when a load was asked to be dismissed.</summary>
public enum DismissOutcome
{
    Dismissed = 1,
    NotFound = 2,
    StillRunning = 3,
}
