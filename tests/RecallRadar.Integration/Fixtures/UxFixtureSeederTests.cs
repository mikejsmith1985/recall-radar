// Checks the fixture actually holds the states the browser suite has to tell apart.
using Microsoft.EntityFrameworkCore;
using RecallRadar.Api.Fixtures;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Integration.Fixtures;

[Collection(PostgresCollection.Name)]
public sealed class UxFixtureSeederTests(PostgresFixture postgres)
{
    private static readonly DateTimeOffset FixedNow = new(2026, 9, 4, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SeedingAnEmptyDatabaseProducesEverySpecTheSuiteNeeds()
    {
        await using var context = await CreateEmptyDatabaseAsync();

        await UxFixtureSeeder.SeedAsync(context, new FixedClock(FixedNow), TestContext.Current.CancellationToken);

        var vehicle = await context.Vehicles.SingleAsync(TestContext.Current.CancellationToken);
        Assert.Equal(UxFixtureSeeder.VehicleDisplayName, vehicle.DisplayName);
        Assert.True(await context.SourceDocuments.CountAsync(
            document => document.Kind == SourceKind.Complaint, TestContext.Current.CancellationToken) >= 1);
        Assert.True(await context.SourceDocuments.AnyAsync(
            document => document.Kind == SourceKind.Investigation, TestContext.Current.CancellationToken));
        Assert.True(await context.SourceDocuments.AnyAsync(
            document => document.Kind == SourceKind.Recall, TestContext.Current.CancellationToken));
        Assert.True(await context.InvestigationLinks.AnyAsync(TestContext.Current.CancellationToken));
        Assert.True(await context.EvaluationRuns.AnyAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TheScriptedQuoteReallyAppearsInASeededRecord()
    {
        // If it did not, every citation would be dropped and the ask spec would assert on an
        // ungrounded answer while appearing to test a grounded one.
        await using var context = await CreateEmptyDatabaseAsync();
        await UxFixtureSeeder.SeedAsync(context, new FixedClock(FixedNow), TestContext.Current.CancellationToken);

        var carriesQuote = await context.SourceDocuments.AnyAsync(
            document => document.Body.Contains(UxFixtureSeeder.VerifiableQuote), TestContext.Current.CancellationToken);
        var carriesFabrication = await context.SourceDocuments.AnyAsync(
            document => document.Body.Contains(UxFixtureSeeder.FabricatedQuote), TestContext.Current.CancellationToken);

        Assert.True(carriesQuote, "the scripted quote must exist verbatim in a seeded record");
        Assert.False(carriesFabrication, "the fabricated quote must exist in no record, so it is dropped");
    }

    [Fact]
    public async Task EveryComplaintIsRetrievableBecauseItHasAChunk()
    {
        await using var context = await CreateEmptyDatabaseAsync();
        await UxFixtureSeeder.SeedAsync(context, new FixedClock(FixedNow), TestContext.Current.CancellationToken);

        var documentCount = await context.SourceDocuments.CountAsync(TestContext.Current.CancellationToken);
        var chunkCount = await context.DocumentChunks.CountAsync(TestContext.Current.CancellationToken);

        Assert.Equal(documentCount, chunkCount);
    }

    [Fact]
    public async Task SeedingIsANoOpWhenRecordsAlreadyExist()
    {
        // Restarting the fixture application must not double every record.
        await using var context = await CreateEmptyDatabaseAsync();
        await UxFixtureSeeder.SeedAsync(context, new FixedClock(FixedNow), TestContext.Current.CancellationToken);
        var afterFirst = await context.SourceDocuments.CountAsync(TestContext.Current.CancellationToken);

        await UxFixtureSeeder.SeedAsync(context, new FixedClock(FixedNow), TestContext.Current.CancellationToken);

        Assert.Equal(afterFirst, await context.SourceDocuments.CountAsync(TestContext.Current.CancellationToken));
    }

    /// <summary>Seeds into its own database, so the shared container's other tests are unaffected.</summary>
    /// <remarks>
    /// A database name cannot be a SQL parameter, so it is built here rather than passed. The name
    /// is a fixed prefix plus a GUID's hexadecimal digits and is asserted to contain nothing else,
    /// which is what makes building it safe. Issued through Npgsql rather than EF Core because
    /// this is schema administration, not data access.
    /// </remarks>
    private async Task<RecallRadarDbContext> CreateEmptyDatabaseAsync()
    {
        var databaseName = "uxfixture_" + Guid.NewGuid().ToString("N");
        Assert.Matches("^[a-z0-9_]+$", databaseName);

        await using (var admin = new Npgsql.NpgsqlConnection(postgres.ConnectionString))
        {
            await admin.OpenAsync(TestContext.Current.CancellationToken);
            await using var create = admin.CreateCommand();
            create.CommandText = "CREATE DATABASE \"" + databaseName + "\"";
            await create.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var builder = new Npgsql.NpgsqlConnectionStringBuilder(postgres.ConnectionString) { Database = databaseName };
        var options = RecallRadarDbContextFactory
            .Configure(new DbContextOptionsBuilder<RecallRadarDbContext>(), builder.ConnectionString).Options;
        var context = new RecallRadarDbContext(options);
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
        return context;
    }

    private sealed class FixedClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
