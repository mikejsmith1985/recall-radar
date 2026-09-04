// Proves a configured key never reaches a log line or a response body (Article IX).
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RecallRadar.Api.Config;
using RecallRadar.Api.Endpoints;

namespace RecallRadar.Integration.Config;

/// <summary>
/// The settings type refuses to render its secrets, but a key can escape other ways: an exception
/// message, a logged request, an error body echoed back. This runs the application with a
/// recognisable fake key and asserts the value appears in nothing the outside world can see.
/// </summary>
[Collection(PostgresCollection.Name)]
public sealed class NoCredentialLeakTests(PostgresFixture postgres)
{
    /// <summary>Distinctive enough that a substring search cannot match it by accident.</summary>
    private const string FakeAnthropicKey = "sk-ant-leakcanary-0000000000000000000000";

    private const string FakeVoyageKey = "pa-leakcanary-1111111111111111111111";

    [Fact]
    public async Task NoConfiguredKeyAppearsInAnyLogLineOrResponseBody()
    {
        var log = new CapturingLoggerProvider();
        using var client = CreateClient(log);

        var bodies = new List<string>
        {
            await Read(await client.GetAsync("/health", TestContext.Current.CancellationToken)),
            await Read(await client.GetAsync($"/api/search?vehicleId=1&q=exhaust&mode=dense", TestContext.Current.CancellationToken)),
            await Read(await client.PostAsJsonAsync(
                "/api/ask", new AskRequest(1, "exhaust smell?", null), TestContext.Current.CancellationToken)),
            await Read(await client.GetAsync("/api/documents/1", TestContext.Current.CancellationToken)),
            await Read(await client.GetAsync("/api/vehicles", TestContext.Current.CancellationToken)),
        };

        foreach (var body in bodies)
        {
            Assert.DoesNotContain(FakeAnthropicKey, body, StringComparison.Ordinal);
            Assert.DoesNotContain(FakeVoyageKey, body, StringComparison.Ordinal);
        }

        var written = log.Lines;
        Assert.DoesNotContain(written, line => line.Contains(FakeAnthropicKey, StringComparison.Ordinal));
        Assert.DoesNotContain(written, line => line.Contains(FakeVoyageKey, StringComparison.Ordinal));
    }

    [Fact]
    public async Task HealthReportsTheKeysArePresentWithoutRevealingThem()
    {
        var log = new CapturingLoggerProvider();
        using var client = CreateClient(log);

        var body = await Read(await client.GetAsync("/health", TestContext.Current.CancellationToken));

        // Capability is reported; the values are not.
        Assert.Contains("\"answering\":\"ok\"", body, StringComparison.Ordinal);
        Assert.DoesNotContain(FakeAnthropicKey, body, StringComparison.Ordinal);
    }

    private static async Task<string> Read(HttpResponseMessage response) => await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);

    private HttpClient CreateClient(CapturingLoggerProvider log) =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting(AppSettings.ConnectionConfigurationKey, postgres.ConnectionString);
                builder.UseSetting(AppSettings.AnthropicKeyVariable, FakeAnthropicKey);
                builder.UseSetting(AppSettings.VoyageKeyVariable, FakeVoyageKey);
                builder.ConfigureServices(services =>
                {
                    services.AddLogging(logging => logging.SetMinimumLevel(LogLevel.Trace));
                    services.AddSingleton<ILoggerProvider>(log);
                });
            })
            .CreateClient();

    /// <summary>Captures every log line the application writes during the test.</summary>
    private sealed class CapturingLoggerProvider : ILoggerProvider
    {
        private readonly List<string> _lines = [];

        public IReadOnlyList<string> Lines
        {
            get
            {
                lock (_lines)
                {
                    return [.. _lines];
                }
            }
        }

        public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

        public void Dispose()
        {
        }

        private void Add(string line)
        {
            lock (_lines)
            {
                _lines.Add(line);
            }
        }

        private sealed class CapturingLogger(CapturingLoggerProvider owner) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(
                LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                owner.Add(formatter(state, exception));
                if (exception is not null)
                {
                    owner.Add(exception.ToString());
                }
            }
        }
    }
}
