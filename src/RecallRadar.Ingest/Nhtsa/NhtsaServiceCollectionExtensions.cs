// Registers the NHTSA clients with the framework's standard resilience pipeline.
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using RecallRadar.Ingest.Config;

namespace RecallRadar.Ingest.Nhtsa;

/// <summary>
/// Framework-first (Article VII): retries, per-attempt timeouts, a total timeout and a circuit
/// breaker all come from <c>AddStandardResilienceHandler</c>. The only tuning is longer timeouts,
/// because the complaints response for a popular vehicle is over a megabyte and the flat file is
/// several, and NHTSA is not fast.
/// </summary>
public static class NhtsaServiceCollectionExtensions
{
    public static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan TotalTimeout = TimeSpan.FromMinutes(4);

    /// <summary>Adds the four typed clients, each with its base address taken from <see cref="IngestOptions"/>.</summary>
    public static IServiceCollection AddNhtsaClients(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddHttpClient<NhtsaComplaintsClient>(ConfigureApiClient).AddStandardResilienceHandler(ConfigureResilience);
        services.AddHttpClient<NhtsaRecallsClient>(ConfigureApiClient).AddStandardResilienceHandler(ConfigureResilience);
        services.AddHttpClient<NhtsaModelsClient>(ConfigureApiClient).AddStandardResilienceHandler(ConfigureResilience);
        services.AddHttpClient<NhtsaFlatFileClient>().AddStandardResilienceHandler(ConfigureResilience);
        return services;
    }

    private static void ConfigureApiClient(IServiceProvider provider, HttpClient client)
    {
        var options = provider.GetRequiredService<IOptions<IngestOptions>>().Value;
        client.BaseAddress = new Uri(options.NhtsaApiBaseUrl, UriKind.Absolute);
    }

    /// <summary>The circuit breaker's sampling window must be at least twice the attempt timeout, so it moves with it.</summary>
    private static void ConfigureResilience(HttpStandardResilienceOptions options)
    {
        options.AttemptTimeout.Timeout = AttemptTimeout;
        options.TotalRequestTimeout.Timeout = TotalTimeout;
        options.CircuitBreaker.SamplingDuration = AttemptTimeout * 2;
    }
}
