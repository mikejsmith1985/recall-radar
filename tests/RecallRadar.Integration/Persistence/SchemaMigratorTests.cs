// Checks the API brings its own database up to date, and reports honestly when it has not.
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RecallRadar.Api.Config;
using RecallRadar.Api.Endpoints;
using RecallRadar.Api.Persistence;

namespace RecallRadar.Integration.Persistence;

/// <summary>
/// The API needs tables only its own migrations create, and for a while nothing applied them: the
/// ingest command line did, the API did not. A database left one migration behind answered every
/// health probe with "ok" and then returned 500 the moment somebody added a vehicle, because
/// <c>ingest_jobs</c> did not exist. Reachable is not the same as correct.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class SchemaMigratorTests(PostgresFixture postgres)
{
    [Fact]
    public async Task TheApiBringsADatabaseThatIsBehindUpToDate()
    {
        var connectionString = await postgres.CreateDatabaseOneMigrationBehindAsync("behind_and_migrated");
        await using var context = PostgresFixture.CreateContext(connectionString);
        Assert.NotEmpty(await SchemaMigrator.FindPendingAsync(context, TestContext.Current.CancellationToken));

        await using var factory = CreateFactory(connectionString, shouldMigrateAtStartup: true);
        using var client = factory.CreateClient();
        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await SchemaMigrator.FindPendingAsync(context, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task HealthCallsOutASchemaThatIsBehindRatherThanReportingOk()
    {
        var connectionString = await postgres.CreateDatabaseOneMigrationBehindAsync("behind_and_left_behind");
        await using var factory = CreateFactory(connectionString, shouldMigrateAtStartup: false);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        var report = await response.Content.ReadFromJsonAsync<HealthResponse>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(HealthEndpoints.SchemaBehind, report!.Database);
        Assert.Equal(HealthEndpoints.Unavailable, report.Status);
    }

    [Fact]
    public async Task AnEmptyDatabaseIsBuiltFromNothing()
    {
        // The first run on a new machine has no schema at all, not merely an old one.
        var connectionString = await postgres.CreateEmptyDatabaseAsync("empty_at_startup");
        await using var factory = CreateFactory(connectionString, shouldMigrateAtStartup: true);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        var report = await response.Content.ReadFromJsonAsync<HealthResponse>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HealthEndpoints.Available, report!.Database);
        // Health saying so is a claim; the schema being there is the evidence.
        await using var context = PostgresFixture.CreateContext(connectionString);
        Assert.Empty(await SchemaMigrator.FindPendingAsync(context, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheTableTheApiOwnsIsQueryableOnADatabaseThatStartedOutBehind()
    {
        // The reported failure was a 500 from the loads list, whose table the newest migration
        // creates. Health going green is a claim; reading the table is the evidence.
        var connectionString = await postgres.CreateDatabaseOneMigrationBehindAsync("behind_then_queried");
        await using var factory = CreateFactory(connectionString, shouldMigrateAtStartup: true);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/loads", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AnUnreachableDatabaseLeavesTheProcessRunningSoItCanSayWhatIsWrong()
    {
        // Exiting at startup would be a process that dies before anyone can ask it what happened.
        await using var factory = CreateFactory(
            "Host=127.0.0.1;Port=1;Database=nowhere;Username=nobody;Password=none;Timeout=1",
            shouldMigrateAtStartup: true);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);
        var report = await response.Content.ReadFromJsonAsync<HealthResponse>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(HealthEndpoints.Unavailable, report!.Database);
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString, bool shouldMigrateAtStartup) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(AppSettings.ConnectionConfigurationKey, connectionString);
            builder.UseSetting(RunnerOff.Key, RunnerOff.Value);
            builder.UseSetting(
                $"{SchemaOptions.SectionName}:{nameof(SchemaOptions.ShouldMigrateAtStartup)}",
                shouldMigrateAtStartup ? "true" : "false");
        });
}
