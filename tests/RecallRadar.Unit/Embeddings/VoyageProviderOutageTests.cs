// Checks that a provider that is down or erroring is reported as an outage, never as a raw failure.
using System.Net;
using System.Text;
using Microsoft.Extensions.AI;
using RecallRadar.Retrieval.Embeddings;

namespace RecallRadar.Unit.Embeddings;

/// <summary>
/// A provider outage once escaped as an unhandled HTTP exception, which the API turned into a 500.
/// Keyword search still works during an outage, so it has to be a reportable state instead.
/// </summary>
public sealed class VoyageProviderOutageTests
{
    private const string BaseAddress = "https://voyage.invalid/";

    [Fact]
    public async Task AnUnreachableProviderBecomesATypedOutage()
    {
        using var generator = BuildGenerator(new ThrowingHandler(new HttpRequestException("refused")));

        var failure = await Assert.ThrowsAsync<EmbeddingProviderUnavailableException>(
            () => generator.GenerateAsync(["exhaust odor"], options: null, TestContext.Current.CancellationToken));

        Assert.Contains("mode=sparse", failure.Message, StringComparison.Ordinal);
        Assert.IsType<HttpRequestException>(failure.InnerException);
    }

    [Fact]
    public async Task ATimeoutBecomesATypedOutageRatherThanACancellation()
    {
        using var generator = BuildGenerator(new ThrowingHandler(new TaskCanceledException("timed out")));

        await Assert.ThrowsAsync<EmbeddingProviderUnavailableException>(
            () => generator.GenerateAsync(["exhaust odor"], options: null, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.BadGateway)]
    [InlineData(HttpStatusCode.TooManyRequests)]
    public async Task AnUnsuccessfulStatusBecomesATypedOutageNamingTheCode(HttpStatusCode status)
    {
        using var generator = BuildGenerator(new StatusHandler(status));

        var failure = await Assert.ThrowsAsync<EmbeddingProviderUnavailableException>(
            () => generator.GenerateAsync(["exhaust odor"], options: null, TestContext.Current.CancellationToken));

        Assert.Contains(((int)status).ToString(), failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ARejectedCredentialStaysAConfigurationProblemNotAnOutage()
    {
        using var generator = BuildGenerator(new StatusHandler(HttpStatusCode.Unauthorized));

        await Assert.ThrowsAsync<EmbeddingsUnavailableException>(
            () => generator.GenerateAsync(["exhaust odor"], options: null, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ACallerCancellationIsStillACancellationNotAnOutage()
    {
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        using var generator = BuildGenerator(new ThrowingHandler(new TaskCanceledException("cancelled")));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => generator.GenerateAsync(["exhaust odor"], options: null, cancellation.Token));
    }

    private static VoyageEmbeddingGenerator BuildGenerator(HttpMessageHandler handler) =>
        new(new HttpClient(handler) { BaseAddress = new Uri(BaseAddress) });

    private sealed class ThrowingHandler(Exception failure) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromException<HttpResponseMessage>(failure);
    }

    private sealed class StatusHandler(HttpStatusCode status) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(status)
            {
                // A body is supplied deliberately: the generator must not quote it, because an
                // error body can echo the credential back.
                Content = new StringContent("{\"error\":\"secret-echo\"}", Encoding.UTF8, "application/json"),
            });
    }
}
