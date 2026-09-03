// Starts one throwaway pgvector Postgres container for the integration collection and migrates it.
using Microsoft.EntityFrameworkCore;
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

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        await using var context = CreateContext();
        await context.Database.MigrateAsync();
    }

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();

    /// <summary>Opens a fresh context against the container with the production Npgsql + pgvector wiring.</summary>
    public RecallRadarDbContext CreateContext()
    {
        var options = RecallRadarDbContextFactory
            .Configure(new DbContextOptionsBuilder<RecallRadarDbContext>(), ConnectionString)
            .Options;
        return new RecallRadarDbContext(options);
    }
}

[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "postgres";
}
