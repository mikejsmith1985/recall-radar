// The record of one load: what it was asked to fetch, whether it ran, and how it ended.
namespace RecallRadar.Retrieval.Persistence;

/// <summary>Where a load came from. Kept so a scheduled refresh is distinguishable from a person asking.</summary>
public enum IngestTrigger
{
    Manual = 1,
    Scheduled = 2,
}

/// <summary>How far a load has got. A job only ever moves forwards through these.</summary>
public enum IngestJobState
{
    Queued = 1,
    Running = 2,
    Succeeded = 3,
    Failed = 4,
}

/// <summary>
/// One request to load a vehicle's NHTSA records, and what became of it.
/// </summary>
/// <remarks>
/// Loading takes minutes and reaches three external feeds, so it cannot happen inside the request
/// that asks for it. This row is what the caller polls instead. It carries the NHTSA identifiers
/// itself rather than reading them from configuration, so a vehicle added through the API is loaded
/// by exactly the names it was registered with, and a later refresh repeats that same lookup.
/// </remarks>
public sealed class IngestJob
{
    /// <summary>Long failures are truncated rather than dropped: a stored reason beats a lost row.</summary>
    public const int MaximumMessageLength = 2000;

    public long Id { get; private set; }

    /// <summary>The vehicle the records landed in. Null until the load succeeds, because it may not exist yet.</summary>
    public int? VehicleId { get; private set; }

    public string Make { get; private set; } = string.Empty;
    public string NhtsaModel { get; private set; } = string.Empty;

    /// <summary>The name the recalls feed accepts, when it differs from the complaints feed's.</summary>
    public string? RecallModel { get; private set; }

    public int ModelYear { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;
    public IngestTrigger Trigger { get; private set; }
    public IngestJobState State { get; private set; }
    public DateTimeOffset QueuedAt { get; private set; }
    public DateTimeOffset? StartedAt { get; private set; }
    public DateTimeOffset? FinishedAt { get; private set; }

    /// <summary>Why the load failed. Null while it is running and on success.</summary>
    public string? Message { get; private set; }

    /// <summary>The counts the load produced, as written by the ingest report. Empty until it succeeds.</summary>
    public string ReportJson { get; private set; } = string.Empty;

    private IngestJob() { }

    /// <summary>True once the job can no longer change, so a poller can stop asking.</summary>
    public bool IsFinished => State is IngestJobState.Succeeded or IngestJobState.Failed;

    /// <summary>
    /// Records a request to load a vehicle. NHTSA identifiers are upper-cased here because every
    /// later comparison against the feeds is an exact match.
    /// </summary>
    public static IngestJob Queue(
        string make,
        string nhtsaModel,
        string? recallModel,
        int modelYear,
        string displayName,
        IngestTrigger trigger,
        DateTimeOffset queuedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(make);
        ArgumentException.ThrowIfNullOrWhiteSpace(nhtsaModel);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (modelYear < Vehicle.MinimumModelYear)
        {
            throw new ArgumentOutOfRangeException(
                nameof(modelYear), $"Model year must be {Vehicle.MinimumModelYear} or later.");
        }

        return new IngestJob
        {
            Make = make.Trim().ToUpperInvariant(),
            NhtsaModel = nhtsaModel.Trim().ToUpperInvariant(),
            RecallModel = string.IsNullOrWhiteSpace(recallModel) ? null : recallModel.Trim().ToUpperInvariant(),
            ModelYear = modelYear,
            DisplayName = displayName.Trim(),
            Trigger = trigger,
            State = IngestJobState.Queued,
            QueuedAt = queuedAt,
        };
    }

    /// <summary>Claims the job. Throws if it was not waiting, so two runners cannot load it twice.</summary>
    public void Start(DateTimeOffset startedAt)
    {
        if (State != IngestJobState.Queued)
        {
            throw new InvalidOperationException($"Job {Id} is {State}, so it cannot be started.");
        }

        State = IngestJobState.Running;
        StartedAt = startedAt;
    }

    /// <summary>Records a completed load and the vehicle its records landed in.</summary>
    public void Succeed(int vehicleId, string reportJson, DateTimeOffset finishedAt)
    {
        State = IngestJobState.Succeeded;
        VehicleId = vehicleId;
        ReportJson = reportJson ?? string.Empty;
        FinishedAt = finishedAt;
        Message = null;
    }

    /// <summary>Records why the load failed, truncated to what the column holds.</summary>
    public void Fail(string message, DateTimeOffset finishedAt)
    {
        State = IngestJobState.Failed;
        Message = Truncate(message);
        FinishedAt = finishedAt;
    }

    private static string Truncate(string message)
    {
        var text = message ?? string.Empty;
        return text.Length <= MaximumMessageLength ? text : text[..MaximumMessageLength];
    }
}
