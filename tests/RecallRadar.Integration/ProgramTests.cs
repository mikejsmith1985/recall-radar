// Hosts the real API in-process against the container and checks the health endpoint reflects the database.
using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;
using RecallRadar.Api.Config;

namespace RecallRadar.Integration;

[Collection(PostgresCollection.Name)]
public sealed class ProgramTests(PostgresFixture postgres)
{
    [Fact]
    public async Task Health_ReportsHealthyWhenTheDatabaseIsReachable()
    {
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting(AppSettings.ConnectionConfigurationKey, postgres.ConnectionString));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Health_ReportsUnhealthyWhenTheDatabaseIsUnreachable()
    {
        const string unreachable = "Host=127.0.0.1;Port=1;Database=nowhere;Username=nobody;Password=none;Timeout=1";
        await using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting(AppSettings.ConnectionConfigurationKey, unreachable));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal("Unhealthy", await response.Content.ReadAsStringAsync());
    }
}
