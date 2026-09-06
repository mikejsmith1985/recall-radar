// Checks the schedule queues a refresh for stale vehicles only, and stays off unless asked for.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RecallRadar.Api.Loading;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Integration.Loading;

[Collection(PostgresCollection.Name)]
public sealed class ScheduledRefreshServiceTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Now = new(2026, 9, 6, 12, 0, 0, TimeSpan.Zero);
    private static readonly TimeSpan StaleAfter = TimeSpan.FromHours(24);

    [Fact]
    public async Task AVehicleWithNoLoadAtAllIsPutOnTheSchedule()
    {
        // A vehicle loaded from the command line has no job row, and would otherwise never refresh.
        var vehicle = await SeedVehicleAsync();

        await RunOnceAsync(enabled: true);

        Assert.True(await HasQueuedRefreshAsync(vehicle.DisplayName));
    }

    [Fact]
    public async Task AVehicleLoadedRecentlyIsLeftAlone()
    {
        var vehicle = await SeedVehicleAsync();
        await SeedSucceededLoadAsync(vehicle, finishedAt: Now.AddHours(-1));

        await RunOnceAsync(enabled: true);

        Assert.False(await HasQueuedRefreshAsync(vehicle.DisplayName));
    }

    [Fact]
    public async Task AVehicleWhoseLastLoadHasGoneStaleIsRefreshed()
    {
        var vehicle = await SeedVehicleAsync();
        await SeedSucceededLoadAsync(vehicle, finishedAt: Now - StaleAfter - TimeSpan.FromMinutes(1));

        await RunOnceAsync(enabled: true);

        Assert.True(await HasQueuedRefreshAsync(vehicle.DisplayName));
    }

    [Fact]
    public async Task AVehicleWithALoadAlreadyWaitingIsNotQueuedTwice()
    {
        // Two refreshes for one vehicle would be two passes over feeds that belong to somebody else.
        var vehicle = await SeedVehicleAsync();
        await RunOnceAsync(enabled: true);

        await RunOnceAsync(enabled: true);

        Assert.Equal(1, await CountQueuedRefreshesAsync(vehicle.DisplayName));
    }

    [Fact]
    public async Task NothingIsQueuedWhenTheScheduleIsOff()
    {
        // A background process that reaches the internet has to be opted into.
        var vehicle = await SeedVehicleAsync();

        await RunOnceAsync(enabled: false);

        Assert.False(await HasQueuedRefreshAsync(vehicle.DisplayName));
    }

    [Fact]
    public async Task TheRefreshRepeatsTheVehiclesOwnNhtsaNames()
    {
        // Including the recalls name, which differs for a truck. Reading it from the vehicle is what
        // lets a vehicle added through the API refresh without any configuration file.
        var vehicle = Vehicle.Create(
            "FORD", $"F-150 SUPER CREW {Guid.NewGuid():N}", 2014, $"Refresh names {Guid.NewGuid():N}", "F-150");

        var registration = ScheduledRefreshService.ToRegistration(vehicle);

        Assert.Equal(vehicle.NhtsaModel, registration.NhtsaModel);
        Assert.Equal("F-150", registration.RecallModel);
        Assert.Equal(vehicle.DisplayName, registration.DisplayName);
        Assert.Equal(2014, registration.ModelYear);
    }

    /// <summary>
    /// Drives exactly one pass. Waiting a fixed time for the background loop instead would make the
    /// result depend on how many vehicles the shared container happened to hold.
    /// </summary>
    private async Task RunOnceAsync(bool enabled)
    {
        await using var provider = BuildProvider();
        var service = new ScheduledRefreshService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            new FixedClock(Now),
            Options.Create(new ScheduledRefreshOptions
            {
                IsEnabled = enabled,
                Interval = StaleAfter,
                CheckInterval = TimeSpan.FromMinutes(30),
            }),
            NullLogger<ScheduledRefreshService>.Instance);

        await service.QueueStaleVehiclesAsync(TestContext.Current.CancellationToken);
    }

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(new FixedClock(Now));
        services.AddDbContext<RecallRadarDbContext>(
            options => RecallRadarDbContextFactory.Configure(options, postgres.ConnectionString));
        services.AddScoped<IngestJobQueue>();
        return services.BuildServiceProvider();
    }

    private async Task<Vehicle> SeedVehicleAsync()
    {
        await using var context = postgres.CreateContext();
        var vehicle = Vehicle.Create(
            "FORD", $"SCHED-{Guid.NewGuid():N}", 2013, $"Schedule fixture {Guid.NewGuid():N}");
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return vehicle;
    }

    private async Task SeedSucceededLoadAsync(Vehicle vehicle, DateTimeOffset finishedAt)
    {
        await using var context = postgres.CreateContext();
        var job = IngestJob.Queue(
            vehicle.Make, vehicle.NhtsaModel, vehicle.RecallModel, vehicle.ModelYear,
            vehicle.DisplayName, IngestTrigger.Manual, finishedAt.AddMinutes(-3));
        job.Start(finishedAt.AddMinutes(-3));
        job.Succeed(vehicle.Id, "{}", finishedAt);
        context.IngestJobs.Add(job);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private async Task<bool> HasQueuedRefreshAsync(string displayName) =>
        await CountQueuedRefreshesAsync(displayName) > 0;

    private async Task<int> CountQueuedRefreshesAsync(string displayName)
    {
        await using var context = postgres.CreateContext();
        return await context.IngestJobs.AsNoTracking().CountAsync(
            job => job.DisplayName == displayName && job.Trigger == IngestTrigger.Scheduled,
            TestContext.Current.CancellationToken);
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
