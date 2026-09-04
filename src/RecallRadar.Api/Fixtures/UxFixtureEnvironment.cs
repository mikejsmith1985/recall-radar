// Names the browser-suite environment and prepares its database before the first request.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Api.Fixtures;

/// <summary>
/// The environment the Cypress suite runs the application in. Separate from Development so a
/// developer's own database is never seeded or reset by running the browser tests.
/// </summary>
public static class UxFixtureEnvironment
{
    /// <summary>Set as ASPNETCORE_ENVIRONMENT by scripts/run-dev-clean.ps1 -CypressOnly.</summary>
    public const string Name = "UxFixture";

    /// <summary>Applies migrations and seeds the fixture, before the server accepts a request.</summary>
    public static async Task PrepareAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(services);

        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<RecallRadarDbContext>();
        await database.Database.MigrateAsync(cancellationToken);
        await UxFixtureSeeder.SeedAsync(
            database, scope.ServiceProvider.GetRequiredService<TimeProvider>(), cancellationToken);
    }
}
