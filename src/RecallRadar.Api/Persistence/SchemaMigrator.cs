// Brings the database the API opens up to the schema this build of the API expects.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Api.Persistence;

/// <summary>Whether this process may change the schema of the database it opens.</summary>
public sealed class SchemaOptions
{
    public const string SectionName = "Schema";

    /// <summary>
    /// Whether pending migrations are applied when the API starts. On by default, because the API
    /// owns tables no other component creates: leaving it to a separate command meant a database
    /// could be reachable, report healthy, and still answer 500 the moment somebody added a vehicle.
    /// Turning it off suits a deployment where a person applies migrations deliberately; health then
    /// reports the schema as behind rather than letting the process pretend it can serve.
    /// </summary>
    public bool ShouldMigrateAtStartup { get; init; } = true;
}

/// <summary>Applies pending migrations at startup, and tells health what is still outstanding.</summary>
public static class SchemaMigrator
{
    /// <summary>Applies every pending migration, unless this process is configured not to.</summary>
    /// <remarks>
    /// A failure here is logged, never thrown. A process that exits before it can be asked what went
    /// wrong is worse than one that starts and reports the schema as behind through /health, which
    /// is the same answer it gives when the database cannot be reached at all.
    /// </remarks>
    public static async Task ApplyAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger(nameof(SchemaMigrator));
        if (!services.GetRequiredService<IOptions<SchemaOptions>>().Value.ShouldMigrateAtStartup)
        {
            logger.LogInformation("Startup migration is off; the schema stays whatever the database already holds.");
            return;
        }

        try
        {
            await using var scope = services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<RecallRadarDbContext>();
            var pending = await FindPendingAsync(database, cancellationToken);
            if (pending.Count == 0)
            {
                return;
            }

            logger.LogInformation("Applying {Count} pending migration(s): {Migrations}.", pending.Count, string.Join(", ", pending));
            await database.Database.MigrateAsync(cancellationToken);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            logger.LogError(failure, "The database could not be brought up to date; /health will report the schema as behind.");
        }
    }

    /// <summary>The migrations this build expects that the open database has not applied.</summary>
    public static async Task<IReadOnlyList<string>> FindPendingAsync(
        RecallRadarDbContext database, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        return [.. await database.Database.GetPendingMigrationsAsync(cancellationToken)];
    }
}
