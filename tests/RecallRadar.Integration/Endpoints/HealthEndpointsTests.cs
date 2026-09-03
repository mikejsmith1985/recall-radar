// Checks that health reports capability, not just liveness, against a real database.
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RecallRadar.Api.Config;
using RecallRadar.Api.Endpoints;

namespace RecallRadar.Integration.Endpoints;

[Collection(PostgresCollection.Name)]
public sealed class HealthEndpointsTests(PostgresFixture postgres)
{
    private const string UnreachableDatabase =
        "Host=127.0.0.1;Port=1;Database=nowhere;Username=nobody;Password=none;Timeout=1";

    [Fact]
    public async Task Health_ReportsOkAndNamesTheFeaturesThatNeedKeys()
    {
        await using var factory = CreateFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        var report = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HealthEndpoints.Available, report!.Status);
        Assert.Equal(HealthEndpoints.Available, report.Database);
        // Neither key is set in the test host, which is the state the client must handle today.
        Assert.Equal(HealthEndpoints.Unavailable, report.Embeddings);
        Assert.Equal(HealthEndpoints.Unavailable, report.Answering);
    }

    [Fact]
    public async Task Health_ReportsUnavailableWhenTheDatabaseCannotBeReached()
    {
        await using var factory = CreateFactory(UnreachableDatabase);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");
        var report = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(HealthEndpoints.Unavailable, report!.Database);
        Assert.Equal(HealthEndpoints.Unavailable, report.Status);
    }

    [Fact]
    public async Task Health_IsAlwaysJsonSoTheClientCanReadTheCapabilityFlags()
    {
        await using var factory = CreateFactory(postgres.ConnectionString);
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    private static WebApplicationFactory<Program> CreateFactory(string connectionString) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.UseSetting(AppSettings.ConnectionConfigurationKey, connectionString));
}
