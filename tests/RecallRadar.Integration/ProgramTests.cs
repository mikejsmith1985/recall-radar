// Checks that the host itself starts and serves, independently of what any one endpoint returns.
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using RecallRadar.Api.Config;

namespace RecallRadar.Integration;

/// <summary>
/// Composition-level checks. Endpoint behaviour lives beside each endpoint in
/// <c>Endpoints/</c>; what is proven here is that the application builds its services and routes
/// at all, which is where a bad registration shows up first.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class ProgramTests(PostgresFixture postgres)
{
    [Fact]
    public async Task TheHostStartsAndServesItsRoutes()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AnUnmappedPathIsNotFoundRatherThanAnError()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/nothing-here", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SearchResolvesEveryServiceItDependsOn()
    {
        // A missing registration would surface here as a 500 before any query runs.
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/search?vehicleId=-1&q=exhaust&mode=sparse", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting(AppSettings.ConnectionConfigurationKey, postgres.ConnectionString));
}
