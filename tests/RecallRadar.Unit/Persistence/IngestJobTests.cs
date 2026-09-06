// Checks the record of a load: what it was asked to fetch, and how it ended.
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Unit.Persistence;

public sealed class IngestJobTests
{
    private static readonly DateTimeOffset QueuedAt = new(2026, 9, 6, 9, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset StartedAt = QueuedAt.AddSeconds(2);
    private static readonly DateTimeOffset FinishedAt = QueuedAt.AddMinutes(3);

    [Fact]
    public void Queue_RecordsWhatToLoadAndStartsUnclaimed()
    {
        var job = IngestJob.Queue("Ford", "EXPLORER", null, 2013, "2013 Explorer Sport", IngestTrigger.Manual, QueuedAt);

        Assert.Equal(IngestJobState.Queued, job.State);
        Assert.Equal("FORD", job.Make);
        Assert.Equal("EXPLORER", job.NhtsaModel);
        Assert.Equal("2013 Explorer Sport", job.DisplayName);
        Assert.Equal(IngestTrigger.Manual, job.Trigger);
        Assert.Equal(QueuedAt, job.QueuedAt);
        Assert.Null(job.StartedAt);
        Assert.Null(job.FinishedAt);
    }

    [Fact]
    public void Queue_NormalisesTheNhtsaNamesBecauseEveryLaterComparisonIsExact()
    {
        var job = IngestJob.Queue(" ford ", " f-150 super crew ", " f-150 ", 2014, " 2014 Raptor ", IngestTrigger.Manual, QueuedAt);

        Assert.Equal("FORD", job.Make);
        Assert.Equal("F-150 SUPER CREW", job.NhtsaModel);
        Assert.Equal("F-150", job.RecallModel);
        Assert.Equal("2014 Raptor", job.DisplayName);
    }

    [Fact]
    public void Queue_LeavesTheRecallModelUnsetWhenOneNameServesBothFeeds()
    {
        var job = IngestJob.Queue("Ford", "EXPLORER", "   ", 2013, "2013 Explorer Sport", IngestTrigger.Manual, QueuedAt);

        Assert.Null(job.RecallModel);
    }

    [Fact]
    public void Start_MarksItRunningSoASecondRunnerLeavesItAlone()
    {
        var job = IngestJob.Queue("Ford", "EXPLORER", null, 2013, "2013 Explorer Sport", IngestTrigger.Manual, QueuedAt);

        job.Start(StartedAt);

        Assert.Equal(IngestJobState.Running, job.State);
        Assert.Equal(StartedAt, job.StartedAt);
    }

    [Fact]
    public void Succeed_RecordsTheCountsAndTheVehicleItLoadedInto()
    {
        var job = IngestJob.Queue("Ford", "EXPLORER", null, 2013, "2013 Explorer Sport", IngestTrigger.Manual, QueuedAt);
        job.Start(StartedAt);

        job.Succeed(vehicleId: 7, """{"complaintsNew":2231}""", FinishedAt);

        Assert.Equal(IngestJobState.Succeeded, job.State);
        Assert.Equal(7, job.VehicleId);
        Assert.Contains("2231", job.ReportJson);
        Assert.Equal(FinishedAt, job.FinishedAt);
        Assert.Null(job.Message);
    }

    [Fact]
    public void Fail_KeepsTheReasonWhereSomeoneCanReadIt()
    {
        // A load that failed silently is indistinguishable from one still running.
        var job = IngestJob.Queue("Ford", "MUSTANGG", null, 2013, "Typo", IngestTrigger.Manual, QueuedAt);
        job.Start(StartedAt);

        job.Fail("NHTSA has no complaint model named 'MUSTANGG'.", FinishedAt);

        Assert.Equal(IngestJobState.Failed, job.State);
        Assert.Contains("MUSTANGG", job.Message);
        Assert.Equal(FinishedAt, job.FinishedAt);
    }

    [Fact]
    public void Fail_TruncatesAnEnormousReasonRatherThanRefusingToStoreIt()
    {
        // Some failures carry a whole HTTP body. Losing the row would lose the only evidence.
        var job = IngestJob.Queue("Ford", "EXPLORER", null, 2013, "2013 Explorer Sport", IngestTrigger.Manual, QueuedAt);
        job.Start(StartedAt);

        job.Fail(new string('x', IngestJob.MaximumMessageLength * 2), FinishedAt);

        Assert.Equal(IngestJob.MaximumMessageLength, job.Message!.Length);
    }

    [Fact]
    public void IsFinished_IsTrueOnlyOnceTheJobCanNoLongerChange()
    {
        var job = IngestJob.Queue("Ford", "EXPLORER", null, 2013, "2013 Explorer Sport", IngestTrigger.Manual, QueuedAt);
        Assert.False(job.IsFinished);

        job.Start(StartedAt);
        Assert.False(job.IsFinished);

        job.Succeed(1, "{}", FinishedAt);
        Assert.True(job.IsFinished);
    }

    [Fact]
    public void Start_RefusesAJobThatIsNotWaitingToRun()
    {
        // Two runners must never both claim the same job and load the same records twice.
        var job = IngestJob.Queue("Ford", "EXPLORER", null, 2013, "2013 Explorer Sport", IngestTrigger.Manual, QueuedAt);
        job.Start(StartedAt);

        Assert.Throws<InvalidOperationException>(() => job.Start(StartedAt));
    }

    [Fact]
    public void Queue_RejectsARegistrationThatCouldNeverBeLookedUp()
    {
        Assert.Throws<ArgumentException>(
            () => IngestJob.Queue("", "EXPLORER", null, 2013, "2013 Explorer Sport", IngestTrigger.Manual, QueuedAt));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => IngestJob.Queue("Ford", "EXPLORER", null, 1800, "Too old", IngestTrigger.Manual, QueuedAt));
    }
}
