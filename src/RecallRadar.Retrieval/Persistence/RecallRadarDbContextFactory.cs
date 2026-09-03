// Builds the DbContext for `dotnet ef` at design time and for the application at run time.
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RecallRadar.Retrieval.Persistence;

/// <summary>
/// Single place that knows how to wire Npgsql with the pgvector type mapping. The design-time
/// path reads <c>RECALLRADAR_CONNECTION</c> and falls back to the docker-compose database, so
/// generating a migration never needs the API project to start.
/// </summary>
public sealed class RecallRadarDbContextFactory : IDesignTimeDbContextFactory<RecallRadarDbContext>
{
    public const string ConnectionEnvironmentVariable = "RECALLRADAR_CONNECTION";

    /// <summary>Matches docker-compose.yml. Local-only credentials, deliberately not a secret.</summary>
    public const string LocalComposeConnection =
        "Host=127.0.0.1;Port=5433;Database=recallradar;Username=recallradar;Password=recallradar-local";

    /// <summary>Configures Npgsql with pgvector for any options builder, so every caller maps vectors the same way.</summary>
    public static DbContextOptionsBuilder<RecallRadarDbContext> Configure(
        DbContextOptionsBuilder<RecallRadarDbContext> builder, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return builder.UseNpgsql(connectionString, npgsql => npgsql.UseVector());
    }

    /// <summary>Applies the same configuration to a non-generic builder, as ASP.NET Core's AddDbContext supplies.</summary>
    public static void Configure(DbContextOptionsBuilder builder, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        builder.UseNpgsql(connectionString, npgsql => npgsql.UseVector());
    }

    /// <summary>Resolves the connection string `dotnet ef` should use.</summary>
    public static string ResolveDesignTimeConnection(Func<string, string?> readEnvironment)
    {
        var configured = readEnvironment(ConnectionEnvironmentVariable);
        return string.IsNullOrWhiteSpace(configured) ? LocalComposeConnection : configured;
    }

    public RecallRadarDbContext CreateDbContext(string[] args)
    {
        var connection = ResolveDesignTimeConnection(Environment.GetEnvironmentVariable);
        var options = Configure(new DbContextOptionsBuilder<RecallRadarDbContext>(), connection).Options;
        return new RecallRadarDbContext(options);
    }
}
