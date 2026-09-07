// Starts one throwaway pgvector Postgres container for the integration collection and migrates it.
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using RecallRadar.Retrieval.Persistence;
using Testcontainers.PostgreSql;

namespace RecallRadar.Integration;

/// <summary>
/// Real infrastructure, never a mocked driver (Article V). The container is shared by every
/// test in the <see cref="PostgresCollection"/> and destroyed when the run ends.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    /// <summary>Same image as docker-compose.yml so the tests exercise the extension the app ships with.</summary>
    public const string Image = "pgvector/pgvector:pg17";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image)
        .Build();

    public string ConnectionString => _container.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _container.StartAsync();
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public ValueTask DisposeAsync() => _container.DisposeAsync();

    /// <summary>Opens a fresh context against the container with the production Npgsql + pgvector wiring.</summary>
    public RecallRadarDbContext CreateContext()
    {
        var options = RecallRadarDbContextFactory
            .Configure(new DbContextOptionsBuilder<RecallRadarDbContext>(), ConnectionString)
            .Options;
        return new RecallRadarDbContext(options);
    }

    /// <summary>
    /// Creates a second database on the same server, migrated only as far as the migration before
    /// the newest one -- a database that is real, reachable, and behind.
    /// </summary>
    /// <remarks>
    /// The shared fixture database is always fully migrated, so nothing hosted against it can ever
    /// meet the state a developer's own machine reaches after pulling a branch that added a table.
    /// Deriving the target from the migration list rather than naming one keeps this about being
    /// behind, so it stays true as migrations are added.
    /// </remarks>
    public async Task<string> CreateDatabaseOneMigrationBehindAsync(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var connectionString = await CreateEmptyDatabaseAsync(name);
        await using var context = CreateContext(connectionString);
        var migrations = context.Database.GetMigrations().ToList();
        if (migrations.Count < 2)
        {
            throw new InvalidOperationException("A database cannot be one migration behind until there are two.");
        }

        await context.GetService<IMigrator>().MigrateAsync(migrations[^2]);
        return connectionString;
    }

    /// <summary>Creates a database with no schema at all on the fixture's server.</summary>
    public async Task<string> CreateEmptyDatabaseAsync(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        await using (var admin = new NpgsqlConnection(ConnectionString))
        {
            await admin.OpenAsync();
            await using var create = new NpgsqlCommand($"CREATE DATABASE \"{name}\"", admin);
            await create.ExecuteNonQueryAsync();
        }

        return new NpgsqlConnectionStringBuilder(ConnectionString) { Database = name }.ConnectionString;
    }

    /// <summary>Opens a context against any database on the fixture's server.</summary>
    public static RecallRadarDbContext CreateContext(string connectionString)
    {
        var options = RecallRadarDbContextFactory
            .Configure(new DbContextOptionsBuilder<RecallRadarDbContext>(), connectionString)
            .Options;
        return new RecallRadarDbContext(options);
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
