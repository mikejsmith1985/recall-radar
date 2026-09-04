// A recorded evaluation: when it ran, over how many cases, and what it measured.
namespace RecallRadar.Retrieval.Persistence;

/// <summary>
/// One run of the evaluation, stored so the numbers can be compared over time rather than
/// asserted once. The metrics are kept as JSON because what is measured will grow, and a
/// historical run should keep whatever shape it was written with.
/// </summary>
public sealed class EvaluationRun
{
    public long Id { get; private set; }

    /// <summary>The vehicle the run covered, or null when it covered every vehicle.</summary>
    public int? VehicleId { get; private set; }

    public DateTimeOffset RanAt { get; private set; }

    /// <summary>How many query-and-relevant-set pairs the run scored.</summary>
    public int CaseCount { get; private set; }

    public string MetricsJson { get; private set; } = string.Empty;

    private EvaluationRun() { }

    /// <summary>Records a run. A run with no cases is still recorded: an empty ground truth is a finding.</summary>
    public static EvaluationRun Create(int? vehicleId, DateTimeOffset ranAt, int caseCount, string metricsJson)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(metricsJson);
        ArgumentOutOfRangeException.ThrowIfNegative(caseCount);

        return new EvaluationRun
        {
            VehicleId = vehicleId,
            RanAt = ranAt,
            CaseCount = caseCount,
            MetricsJson = metricsJson,
        };
    }
}
