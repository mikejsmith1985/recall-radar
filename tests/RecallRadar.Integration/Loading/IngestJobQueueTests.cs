// Checks the waiting list of loads: queueing, claiming exactly once, and finding one again.
using Microsoft.EntityFrameworkCore;
using RecallRadar.Api.Loading;
using RecallRadar.Ingest.Config;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Integration.Loading;

[Collection(PostgresCollection.Name)]
public sealed class IngestJobQueueTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task QueueingRecordsTheRegistrationTheLoadMustRepeat()
    {
        await using var context = postgres.CreateContext();
        var registration = BuildRegistration();

        var job = await new IngestJobQueue(context, new FixedClock(Now))
            .QueueAsync(registration, IngestTrigger.Manual, TestContext.Current.CancellationToken);

        Assert.NotEqual(0, job.Id);
        Assert.Equal(IngestJobState.Queued, job.State);
        Assert.Equal(registration.DisplayName, job.DisplayName);
        Assert.Equal("F-150", job.RecallModel);
        Assert.Equal(Now, job.QueuedAt);
    }

    [Fact]
    public async Task ClaimingTakesTheOldestWaitingJobAndMarksItRunning()
    {
        await using var context = postgres.CreateContext();
        var queue = new IngestJobQueue(context, new FixedClock(Now));
        var mine = BuildRegistration();
        await queue.QueueAsync(mine, IngestTrigger.Manual, TestContext.Current.CancellationToken);

        var claimed = await ClaimUntilAsync(queue, mine.DisplayName);

        Assert.NotNull(claimed);
        Assert.Equal(IngestJobState.Running, claimed.State);
        Assert.Equal(Now, claimed.StartedAt);
    }

    [Fact]
    public async Task AClaimedJobIsNeverHandedOutASecondTime()
    {
        // Two runners claiming the same job would load the same vehicle twice, against feeds that
        // belong to somebody else.
        await using var context = postgres.CreateContext();
        var queue = new IngestJobQueue(context, new FixedClock(Now));
        var mine = BuildRegistration();
        await queue.QueueAsync(mine, IngestTrigger.Manual, TestContext.Current.CancellationToken);

        var first = await ClaimUntilAsync(queue, mine.DisplayName);
        var reclaimed = await ClaimAllAsync(queue);

        Assert.NotNull(first);
        Assert.DoesNotContain(first.Id, reclaimed.Select(job => job.Id));
    }

    [Fact]
    public async Task AnUnfinishedLoadIsVisibleSoASecondRequestJoinsItRatherThanDuplicatingIt()
    {
        await using var context = postgres.CreateContext();
        var queue = new IngestJobQueue(context, new FixedClock(Now));
        var registration = BuildRegistration();

        Assert.False(await queue.HasUnfinishedLoadAsync(registration.DisplayName, TestContext.Current.CancellationToken));
        var job = await queue.QueueAsync(registration, IngestTrigger.Manual, TestContext.Current.CancellationToken);
        Assert.True(await queue.HasUnfinishedLoadAsync(registration.DisplayName, TestContext.Current.CancellationToken));

        job.Succeed(vehicleId: 1, "{}", Now);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        Assert.False(await queue.HasUnfinishedLoadAsync(registration.DisplayName, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheLatestLoadForAVehicleIsTheOneMostRecentlyQueued()
    {
        await using var context = postgres.CreateContext();
        var registration = BuildRegistration();
        var earlier = new IngestJobQueue(context, new FixedClock(Now));
        var later = new IngestJobQueue(context, new FixedClock(Now.AddMinutes(5)));
        await earlier.QueueAsync(registration, IngestTrigger.Manual, TestContext.Current.CancellationToken);
        var second = await later.QueueAsync(registration, IngestTrigger.Scheduled, TestContext.Current.CancellationToken);

        var latest = await later.FindLatestForDisplayNameAsync(
            registration.DisplayName, TestContext.Current.CancellationToken);

        Assert.Equal(second.Id, latest!.Id);
        Assert.Equal(IngestTrigger.Scheduled, latest.Trigger);
    }

    [Fact]
    public async Task ClaimingReturnsNothingWhenTheListIsEmpty()
    {
        await using var context = postgres.CreateContext();
        var queue = new IngestJobQueue(context, new FixedClock(Now));
        await ClaimAllAsync(queue);

        Assert.Null(await queue.ClaimNextAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// Claims until this test's own job comes up. The container is shared, so another test's job
    /// may be waiting first; taking only ours keeps the assertion about our own row.
    /// </summary>
    private static async Task<IngestJob?> ClaimUntilAsync(IngestJobQueue queue, string displayName)
    {
        foreach (var job in await ClaimAllAsync(queue))
        {
            if (job.DisplayName == displayName)
            {
                return job;
            }
        }

        return null;
    }

    private static async Task<List<IngestJob>> ClaimAllAsync(IngestJobQueue queue)
    {
        var claimed = new List<IngestJob>();
        while (await queue.ClaimNextAsync(TestContext.Current.CancellationToken) is { } job)
        {
            claimed.Add(job);
        }

        return claimed;
    }

    private static VehicleRegistration BuildRegistration() => new()
    {
        Make = "FORD",
        NhtsaModel = "F-150 SUPER CREW",
        RecallModel = "F-150",
        ModelYear = 2014,
        DisplayName = $"Queue fixture {Guid.NewGuid():N}",
    };

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
