// Builds the DbContext for `dotnet ef` at design time and for the application at run time.
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RecallRadar.Retrieval.Persistence;

/// <summary>
/// Single place that knows how to wire Npgsql with the pgvector type mapping. The connection
/// string is never written into source: it comes from the <c>RECALLRADAR_CONNECTION</c>
/// environment variable, which the developer sets in a gitignored <c>.env</c> file or the
/// Forge Vault injects (Article IX).
/// </summary>
public sealed class RecallRadarDbContextFactory : IDesignTimeDbContextFactory<RecallRadarDbContext>
{
    public const string ConnectionEnvironmentVariable = "RECALLRADAR_CONNECTION";

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

    /// <summary>
    /// Resolves the connection string from the environment. There is deliberately no default:
    /// a default would have to contain a password, and passwords do not belong in source.
    /// </summary>
    public static string ResolveConnection(Func<string, string?> readEnvironment)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        var configured = readEnvironment(ConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configured))
        {
            throw new InvalidOperationException(
                $"{ConnectionEnvironmentVariable} is not set. Copy .env.example to .env, fill in the values, " +
                "and run through scripts/run-dev-clean.ps1, or export the variable in your shell.");
        }

        return configured;
    }

    public RecallRadarDbContext CreateDbContext(string[] args)
    {
        var connection = ResolveConnection(Environment.GetEnvironmentVariable);
        var options = Configure(new DbContextOptionsBuilder<RecallRadarDbContext>(), connection).Options;
        return new RecallRadarDbContext(options);
    }
}
