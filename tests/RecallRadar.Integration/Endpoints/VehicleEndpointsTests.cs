// Drives registering a vehicle and watching its load over HTTP, against a recorded NHTSA.
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using RecallRadar.Api.Config;
using RecallRadar.Api.Endpoints;
using RecallRadar.Integration.Ingest;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Integration.Endpoints;

[Collection(PostgresCollection.Name)]
public sealed class VehicleEndpointsTests(PostgresFixture postgres) : IDisposable
{
    private readonly NhtsaFixtureServer _nhtsa = new();
    private readonly List<WebApplicationFactory<Program>> _hosts = [];

    public void Dispose()
    {
        // Each host runs its own background services, so leaving one alive would let it keep
        // claiming jobs after the NHTSA stub it was pointed at has gone.
        foreach (var host in _hosts)
        {
            host.Dispose();
        }

        _nhtsa.Dispose();
    }

    [Fact]
    public async Task Register_QueuesTheLoadAndPointsAtSomewhereToWatchIt()
    {
        // The request cannot wait for three NHTSA feeds, so it answers with a job, not a vehicle.
        using var client = CreateClient();
        var request = BuildRequest();

        var response = await client.PostAsJsonAsync("/api/vehicles", request, TestContext.Current.CancellationToken);
        var load = await response.Content.ReadFromJsonAsync<LoadResponse>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal($"/api/loads/{load!.Id}", response.Headers.Location!.ToString());
        Assert.Equal(request.DisplayName, load.DisplayName);
        Assert.Equal("manual", load.Trigger);
        Assert.False(load.IsFinished);
    }

    [Fact]
    public async Task Register_RejectsAModelNhtsaDoesNotKnowAndNamesOnesItDoes()
    {
        // This is the mistake people actually make, and a bare "invalid" would leave them stuck.
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/vehicles", BuildRequest() with { NhtsaModel = "EXPLORRER" }, TestContext.Current.CancellationToken);
        var problem = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("EXPLORRER", problem, StringComparison.Ordinal);
        Assert.Contains("EXPLORER", problem, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", "EXPLORER", 2013, "A name")]
    [InlineData("FORD", "", 2013, "A name")]
    [InlineData("FORD", "EXPLORER", 1800, "A name")]
    [InlineData("FORD", "EXPLORER", 2013, "")]
    public async Task Register_RejectsARegistrationThatCouldNeverBeLookedUp(
        string make, string model, int year, string displayName)
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/vehicles",
            new RegisterVehicleRequest(make, model, null, year, displayName),
            TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Register_AsksTwiceAndJoinsTheLoadAlreadyWaitingRatherThanStartingASecond()
    {
        // Two passes over the same feeds would double someone else's traffic for no new records.
        using var client = CreateClient();
        var request = BuildRequest();

        var first = await client.PostAsJsonAsync("/api/vehicles", request, TestContext.Current.CancellationToken);
        var second = await client.PostAsJsonAsync("/api/vehicles", request, TestContext.Current.CancellationToken);

        var firstLoad = await first.Content.ReadFromJsonAsync<LoadResponse>(TestContext.Current.CancellationToken);
        var secondLoad = await second.Content.ReadFromJsonAsync<LoadResponse>(TestContext.Current.CancellationToken);
        Assert.Equal(firstLoad!.Id, secondLoad!.Id);
    }

    [Fact]
    public async Task ReadLoad_ReturnsTheJobTheRegistrationCreated()
    {
        using var client = CreateClient();
        var queued = await client.PostAsJsonAsync("/api/vehicles", BuildRequest(), TestContext.Current.CancellationToken);
        var load = await queued.Content.ReadFromJsonAsync<LoadResponse>(TestContext.Current.CancellationToken);

        var fetched = await client.GetFromJsonAsync<LoadResponse>(
            $"/api/loads/{load!.Id}", TestContext.Current.CancellationToken);

        Assert.Equal(load.Id, fetched!.Id);
        Assert.Equal(load.DisplayName, fetched.DisplayName);
    }

    [Fact]
    public async Task ReadLoad_AnswersNotFoundForAJobThatDoesNotExist()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/loads/999999999", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ListLoads_ShowsTheOneJustQueued()
    {
        using var client = CreateClient();
        var queued = await client.PostAsJsonAsync("/api/vehicles", BuildRequest(), TestContext.Current.CancellationToken);
        var load = await queued.Content.ReadFromJsonAsync<LoadResponse>(TestContext.Current.CancellationToken);

        var loads = await client.GetFromJsonAsync<List<LoadResponse>>("/api/loads", TestContext.Current.CancellationToken);

        Assert.Contains(loads!, entry => entry.Id == load!.Id);
    }

    [Fact]
    public async Task Refresh_QueuesAnotherPassForAVehicleThatAlreadyExists()
    {
        await using var context = postgres.CreateContext();
        var vehicle = Vehicle.Create("FORD", $"REFRESH-{Guid.NewGuid():N}", 2013, $"Refresh fixture {Guid.NewGuid():N}");
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        using var client = CreateClient();

        var response = await client.PostAsync(
            $"/api/vehicles/{vehicle.Id}/refresh", content: null, TestContext.Current.CancellationToken);
        var load = await response.Content.ReadFromJsonAsync<LoadResponse>(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.Equal(vehicle.DisplayName, load!.DisplayName);
    }

    [Fact]
    public async Task Refresh_AnswersNotFoundForAVehicleThatWasNeverRegistered()
    {
        using var client = CreateClient();

        var response = await client.PostAsync("/api/vehicles/-1/refresh", content: null, TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Register_NeverReachesNhtsaWithAnythingButGet()
    {
        // The application is read-only against NHTSA, and that has to stay true from the API too.
        using var client = CreateClient();

        await client.PostAsJsonAsync("/api/vehicles", BuildRequest(), TestContext.Current.CancellationToken);

        Assert.All(_nhtsa.ReceivedMethods, method => Assert.Equal("GET", method));
    }

    private static RegisterVehicleRequest BuildRequest() => new(
        "Ford", "EXPLORER", null, NhtsaFixtureServer.FixtureModelYear, $"Endpoint fixture {Guid.NewGuid():N}");

    /// <summary>
    /// Hosts the API against the shared container and the recorded NHTSA server. The background
    /// runner is not started here: these tests are about the endpoints, and a runner racing them
    /// would make the assertions depend on how fast a load finished.
    /// </summary>
    private HttpClient CreateClient()
    {
        var host = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting(AppSettings.ConnectionConfigurationKey, postgres.ConnectionString);
            builder.UseSetting("RecallRadar:NhtsaApiBaseUrl", _nhtsa.BaseUrl);
            builder.UseSetting("RecallRadar:InvestigationsFlatFileUrl", _nhtsa.FlatFileUrl);
            builder.UseSetting(RunnerOff.Key, RunnerOff.Value);
        });
        _hosts.Add(host);
        return host.CreateClient();
    }
}
