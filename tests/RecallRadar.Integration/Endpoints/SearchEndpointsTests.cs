// Drives the search endpoints over HTTP against a real database, per contracts/http-api.md.
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RecallRadar.Api.Config;
using RecallRadar.Api.Endpoints;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Integration.Endpoints;

[Collection(PostgresCollection.Name)]
public sealed class SearchEndpointsTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string Component = "ENGINE AND ENGINE COOLING";
    private const string ComplaintBody = "A strong exhaust odor enters the cabin when accelerating hard.";

    private int _vehicleId;
    private string _displayName = string.Empty;

    public async ValueTask InitializeAsync()
    {
        await using var context = postgres.CreateContext();
        _displayName = $"Endpoint fixture {Guid.NewGuid():N}";
        var vehicle = Vehicle.Create("FORD", $"ENDPOINT-{Guid.NewGuid():N}", 2013, _displayName);
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _vehicleId = vehicle.Id;

        var document = SourceDocument.Create(
            SourceKind.Complaint, $"endpoint-{Guid.NewGuid():N}", _vehicleId, Component,
            new DateOnly(2016, 5, 1), "Exhaust odor complaint", ComplaintBody, "{}");
        context.SourceDocuments.Add(document);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.DocumentChunks.Add(DocumentChunk.Create(document.Id, 0, ComplaintBody));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Vehicles_ListsTheFixtureVehicleWithItsRecordCounts()
    {
        using var client = CreateClient();

        var vehicles = await client.GetFromJsonAsync<List<VehicleResponse>>(
            "/api/vehicles", TestContext.Current.CancellationToken);

        var fixture = Assert.Single(vehicles!, vehicle => vehicle.Id == _vehicleId);
        Assert.Equal(_displayName, fixture.DisplayName);
        Assert.Equal("FORD", fixture.Make);
        Assert.True(fixture.Counts.Complaint >= 1);
    }

    [Fact]
    public async Task Search_ReturnsSparseHitsCarryingTheirRankExplanation()
    {
        using var client = CreateClient();

        var response = await client.GetFromJsonAsync<SearchResponse>(
            $"/api/search?vehicleId={_vehicleId}&q=exhaust%20odor&mode=sparse", TestContext.Current.CancellationToken);

        Assert.Equal("sparse", response!.Mode);
        var hit = Assert.Single(response.Hits);
        Assert.Equal("complaint", hit.Kind);
        Assert.Equal(1, hit.SparseRank);
        Assert.Null(hit.DenseRank);
        Assert.True(hit.FusedScore > 0);
        Assert.Contains("exhaust odor", hit.Snippet, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Search_AnswersConflictForModesNeedingEmbeddingsWhenNoneExist()
    {
        using var client = CreateClient();

        foreach (var mode in new[] { "dense", "hybrid" })
        {
            var response = await client.GetAsync($"/api/search?vehicleId={_vehicleId}&q=exhaust&mode={mode}", TestContext.Current.CancellationToken);
            var problem = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            Assert.Contains("sparse", problem, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task Search_AnswersNotFoundForAVehicleThatWasNeverRegistered()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/search?vehicleId=-1&q=exhaust&mode=sparse", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("q=a&mode=sparse")]
    [InlineData("q=exhaust&mode=magic")]
    [InlineData("q=exhaust&mode=sparse&limit=999")]
    public async Task Search_AnswersBadRequestForInputItRejects(string query)
    {
        using var client = CreateClient();

        var response = await client.GetAsync($"/api/search?vehicleId={_vehicleId}&{query}", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Search_DefaultsToHybridWhichIsRefusedWithoutEmbeddings()
    {
        using var client = CreateClient();

        var response = await client.GetAsync($"/api/search?vehicleId={_vehicleId}&q=exhaust", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    private HttpClient CreateClient() =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.UseSetting(AppSettings.ConnectionConfigurationKey, postgres.ConnectionString))
            .CreateClient();
}
