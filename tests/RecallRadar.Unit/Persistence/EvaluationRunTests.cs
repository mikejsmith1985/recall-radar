// Checks the rules on a recorded evaluation run.
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Unit.Persistence;

public sealed class EvaluationRunTests
{
    private static readonly DateTimeOffset RanAt = new(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_KeepsWhatWasMeasured()
    {
        var run = EvaluationRun.Create(vehicleId: 1, RanAt, caseCount: 41, """{"sparse":{}}""");

        Assert.Equal(1, run.VehicleId);
        Assert.Equal(RanAt, run.RanAt);
        Assert.Equal(41, run.CaseCount);
        Assert.Equal("""{"sparse":{}}""", run.MetricsJson);
    }

    [Fact]
    public void Create_AllowsARunCoveringEveryVehicle()
    {
        var run = EvaluationRun.Create(vehicleId: null, RanAt, 41, "{}");

        Assert.Null(run.VehicleId);
    }

    [Fact]
    public void Create_RecordsARunWithNoCasesBecauseAnEmptyGroundTruthIsAFinding()
    {
        var run = EvaluationRun.Create(null, RanAt, caseCount: 0, "{}");

        Assert.Equal(0, run.CaseCount);
    }

    [Fact]
    public void Create_RejectsANegativeCaseCountOrEmptyMetrics()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => EvaluationRun.Create(null, RanAt, -1, "{}"));
        Assert.Throws<ArgumentException>(() => EvaluationRun.Create(null, RanAt, 1, "  "));
    }
}
