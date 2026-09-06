// Drives the ask and document endpoints over HTTP, including the state where no key is configured.
using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RecallRadar.Api.Config;
using RecallRadar.Api.Endpoints;
using RecallRadar.Retrieval.Persistence;

using RecallRadar.Integration;

namespace RecallRadar.Integration.Endpoints;

[Collection(PostgresCollection.Name)]
public sealed class AskEndpointsTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string ComplaintBody = "A STRONG EXHAUST ODOR ENTERS THE CABIN WHEN ACCELERATING HARD.";
    private const string Component = "ENGINE AND ENGINE COOLING";

    private int _vehicleId;
    private long _documentId;

    public async ValueTask InitializeAsync()
    {
        await using var context = postgres.CreateContext();
        var vehicle = Vehicle.Create("FORD", $"ASK-{Guid.NewGuid():N}", 2013, $"Ask fixture {Guid.NewGuid():N}");
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _vehicleId = vehicle.Id;

        var document = SourceDocument.Create(
            SourceKind.Complaint, $"ask-{Guid.NewGuid():N}", _vehicleId, Component,
            new DateOnly(2016, 5, 1), "Exhaust odor complaint", ComplaintBody, "{}");
        context.SourceDocuments.Add(document);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _documentId = document.Id;
        context.DocumentChunks.Add(DocumentChunk.Create(document.Id, 0, ComplaintBody));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task Ask_AnswersServiceUnavailableWhenNoAnsweringKeyIsConfigured()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/ask", new AskRequest(_vehicleId, "exhaust smell?", null), TestContext.Current.CancellationToken);
        var problem = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        // The reader is told what still works rather than only what does not.
        Assert.Contains("Search still works", problem, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Ask_NeverReturnsAnUnhandledServerError()
    {
        using var client = CreateClient();

        foreach (var request in new[]
        {
            new AskRequest(_vehicleId, "exhaust smell?", null),
            new AskRequest(-1, "exhaust smell?", null),
            new AskRequest(_vehicleId, "   ", null),
        })
        {
            var response = await client.PostAsJsonAsync("/api/ask", request, TestContext.Current.CancellationToken);

            Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        }
    }

    [Fact]
    public async Task Document_ReturnsTheRecordVerbatimSoAQuoteCanBeHighlighted()
    {
        using var client = CreateClient();

        var document = await client.GetFromJsonAsync<DocumentResponse>($"/api/documents/{_documentId}", TestContext.Current.CancellationToken);

        Assert.Equal(ComplaintBody, document!.Body);
        Assert.Equal("complaint", document.Kind);
        Assert.Equal(Component, document.Component);
        Assert.Equal(_vehicleId, document.VehicleId);
    }

    [Fact]
    public async Task Document_OffsetsFromACitationSelectTheQuoteInsideTheReturnedBody()
    {
        using var client = CreateClient();
        const string quote = "A STRONG EXHAUST ODOR";

        var document = await client.GetFromJsonAsync<DocumentResponse>($"/api/documents/{_documentId}", TestContext.Current.CancellationToken);
        var start = document!.Body.IndexOf(quote, StringComparison.Ordinal);

        Assert.Equal(quote, document.Body[start..(start + quote.Length)]);
    }

    [Fact]
    public async Task Document_AnswersNotFoundForARecordThatDoesNotExist()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/documents/999999999", TestContext.Current.CancellationToken);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private HttpClient CreateClient() =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(AppSettings.ConnectionConfigurationKey, postgres.ConnectionString);
                builder.UseSetting(RunnerOff.Key, RunnerOff.Value);
            })
            .CreateClient();
}
