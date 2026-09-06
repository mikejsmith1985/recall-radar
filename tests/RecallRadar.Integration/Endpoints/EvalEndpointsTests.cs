// Checks the endpoint that makes the evaluation numbers readable, including before any run exists.
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RecallRadar.Api.Config;
using RecallRadar.Api.Endpoints;
using RecallRadar.Retrieval.Persistence;

using RecallRadar.Integration;

namespace RecallRadar.Integration.Endpoints;

[Collection(PostgresCollection.Name)]
public sealed class EvalEndpointsTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset Earlier = new(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset Later = new(2026, 9, 4, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Eval_ReturnsTheNewestRunAsLatestAndTheRestAsHistory()
    {
        var vehicleId = await SeedVehicleAsync();
        await StoreRunAsync(vehicleId, Earlier, caseCount: 10, """{"sparse":{"metrics":{"recallAt5":0.1}}}""");
        await StoreRunAsync(vehicleId, Later, caseCount: 41, """{"sparse":{"metrics":{"recallAt5":0.6}}}""");

        using var client = CreateClient();
        var response = await client.GetFromJsonAsync<EvaluationsResponse>("/api/eval", TestContext.Current.CancellationToken);

        Assert.NotNull(response!.Latest);
        Assert.Equal(41, response.Latest!.CaseCount);
        Assert.Equal(Later, response.Latest.RanAt);
        Assert.Contains(response.History, run => run.CaseCount == 10);
    }

    [Fact]
    public async Task Eval_ReturnsTheStoredMetricsAsJsonRatherThanAFixedShape()
    {
        // A run written by an older version keeps whatever shape it had, so the endpoint hands the
        // metrics back as they were stored instead of forcing them into today's fields.
        var vehicleId = await SeedVehicleAsync();
        await StoreRunAsync(vehicleId, Later, 5, """{"sparse":{"metrics":{"recallAt5":0.25},"skippedReason":null}}""");

        using var client = CreateClient();
        var response = await client.GetFromJsonAsync<EvaluationsResponse>("/api/eval", TestContext.Current.CancellationToken);

        var sparse = response!.Latest!.Metrics.GetProperty("sparse");
        Assert.Equal(0.25, sparse.GetProperty("metrics").GetProperty("recallAt5").GetDouble());
    }

    [Fact]
    public async Task Eval_AnswersOkWithNoLatestWhenNothingHasBeenMeasuredForAVehicle()
    {
        // A table of zeroes would read as a result. Null reads as "not measured yet", which is true.
        await using var context = postgres.CreateContext();
        var runsExist = context.EvaluationRuns.Any();

        using var client = CreateClient();
        var response = await client.GetAsync("/api/eval", TestContext.Current.CancellationToken);
        var payload = await response.Content.ReadFromJsonAsync<EvaluationsResponse>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        if (!runsExist)
        {
            Assert.Null(payload!.Latest);
            Assert.Empty(payload.History);
        }
    }

    [Fact]
    public async Task MetricsThatAreNotJsonCannotBeStoredAtAll()
    {
        // The column is jsonb, so the database refuses malformed metrics before they can ever be
        // served. That is a stronger guarantee than the endpoint parsing defensively.
        var vehicleId = await SeedVehicleAsync();

        await Assert.ThrowsAsync<DbUpdateException>(
            () => StoreRunAsync(vehicleId, Later.AddDays(1), 1, "this was never valid json"));
    }

    private async Task<int> SeedVehicleAsync()
    {
        await using var context = postgres.CreateContext();
        var vehicle = Vehicle.Create("FORD", $"EVALEP-{Guid.NewGuid():N}", 2013, $"Eval endpoint fixture {Guid.NewGuid():N}");
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return vehicle.Id;
    }

    private async Task StoreRunAsync(int vehicleId, DateTimeOffset ranAt, int caseCount, string metricsJson)
    {
        await using var context = postgres.CreateContext();
        context.EvaluationRuns.Add(EvaluationRun.Create(vehicleId, ranAt, caseCount, metricsJson));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    private HttpClient CreateClient() =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(AppSettings.ConnectionConfigurationKey, postgres.ConnectionString);
                builder.UseSetting(RunnerOff.Key, RunnerOff.Value);
            })
            .CreateClient();
}
