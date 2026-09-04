// Checks that preparing the browser-suite environment leaves a database the specs can run against.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecallRadar.Api.Fixtures;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Integration.Fixtures;

[Collection(PostgresCollection.Name)]
public sealed class UxFixtureEnvironmentTests(PostgresFixture postgres)
{
    [Fact]
    public void TheEnvironmentNameMatchesWhatTheRunnerScriptSets()
    {
        // scripts/run-dev-clean.ps1 -CypressOnly starts the app with this exact value. If the two
        // drifted apart the suite would run against a developer's own database instead.
        Assert.Equal("UxFixture", UxFixtureEnvironment.Name);
    }

    [Fact]
    public async Task PreparingMigratesAndSeedsSoTheFirstRequestFindsData()
    {
        await using var provider = BuildProvider(await CreateEmptyDatabaseNameAsync());

        await UxFixtureEnvironment.PrepareAsync(provider, TestContext.Current.CancellationToken);

        await using var scope = provider.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RecallRadarDbContext>();
        Assert.True(await database.Vehicles.AnyAsync(TestContext.Current.CancellationToken));
        Assert.True(await database.SourceDocuments.AnyAsync(TestContext.Current.CancellationToken));
        Assert.True(await database.EvaluationRuns.AnyAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task PreparingTwiceLeavesTheSameFixture()
    {
        // The application may restart between runs; that must not double every record.
        await using var provider = BuildProvider(await CreateEmptyDatabaseNameAsync());
        await UxFixtureEnvironment.PrepareAsync(provider, TestContext.Current.CancellationToken);

        await using var scope = provider.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RecallRadarDbContext>();
        var afterFirst = await database.SourceDocuments.CountAsync(TestContext.Current.CancellationToken);

        await UxFixtureEnvironment.PrepareAsync(provider, TestContext.Current.CancellationToken);

        Assert.Equal(afterFirst, await database.SourceDocuments.CountAsync(TestContext.Current.CancellationToken));
    }

    private ServiceProvider BuildProvider(string connectionString)
    {
        var services = new ServiceCollection();
        services.AddSingleton(TimeProvider.System);
        services.AddDbContext<RecallRadarDbContext>(
            options => RecallRadarDbContextFactory.Configure(options, connectionString));
        return services.BuildServiceProvider();
    }

    /// <summary>Creates an empty database and returns a connection string pointing at it.</summary>
    private async Task<string> CreateEmptyDatabaseNameAsync()
    {
        var databaseName = "uxenv_" + Guid.NewGuid().ToString("N");
        Assert.Matches("^[a-z0-9_]+$", databaseName);

        await using (var admin = new Npgsql.NpgsqlConnection(postgres.ConnectionString))
        {
            await admin.OpenAsync(TestContext.Current.CancellationToken);
            await using var create = admin.CreateCommand();
            create.CommandText = "CREATE DATABASE \"" + databaseName + "\"";
            await create.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        return new Npgsql.NpgsqlConnectionStringBuilder(postgres.ConnectionString)
        {
            Database = databaseName,
        }.ConnectionString;
    }
}
