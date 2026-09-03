// Registers whichever embedding generator the configured credentials allow.
using System.Net.Http.Headers;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http.Resilience;

namespace RecallRadar.Retrieval.Embeddings;

/// <summary>
/// One place decides whether this process can embed. Both the API and the ingest command call it,
/// so neither can end up with a different answer to "are embeddings available".
/// </summary>
public static class EmbeddingServiceCollectionExtensions
{
    /// <summary>Voyage is not fast on a full batch of long complaint summaries.</summary>
    public static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan TotalTimeout = TimeSpan.FromMinutes(4);

    /// <summary>Configuration key holding the Voyage credential. The vault fills it; no default exists.</summary>
    public const string ApiKeyConfigurationKey = EmbeddingsUnavailableException.ApiKeyVariable;

    /// <summary>Configuration key that overrides the Voyage base address, so tests never call the real one.</summary>
    public const string BaseAddressConfigurationKey = "VOYAGE_BASE_ADDRESS";

    private const string AuthenticationScheme = "Bearer";

    /// <summary>
    /// Registers the Voyage generator when a key is configured, and the refusing
    /// <see cref="NullEmbeddingGenerator"/> when one is not.
    /// </summary>
    /// <remarks>
    /// The key is read here and attached to the typed client's default headers. It is never stored
    /// on a service, so nothing downstream can log it (Article IX).
    /// </remarks>
    public static IServiceCollection AddEmbeddingGenerator(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var apiKey = configuration[ApiKeyConfigurationKey];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            services.AddTransient<IEmbeddingGenerator<string, Embedding<float>>>(_ => new NullEmbeddingGenerator());
            return services;
        }

        var baseAddress = ReadBaseAddress(configuration);
        services
            .AddHttpClient<VoyageEmbeddingGenerator>(client => ConfigureClient(client, baseAddress, apiKey))
            .AddStandardResilienceHandler(ConfigureResilience);

        // Transient, because that is the lifetime a typed client is built for: the handler underneath
        // is pooled and rotated by IHttpClientFactory, and holding one client for the life of the
        // process is how an application ends up with stale DNS.
        services.AddTransient<IEmbeddingGenerator<string, Embedding<float>>>(
            provider => provider.GetRequiredService<VoyageEmbeddingGenerator>());
        return services;
    }

    /// <summary>Whether a key is configured, without building the container to find out.</summary>
    public static bool HasEmbeddingKey(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        return !string.IsNullOrWhiteSpace(configuration[ApiKeyConfigurationKey]);
    }

    private static string ReadBaseAddress(IConfiguration configuration)
    {
        var configured = configuration[BaseAddressConfigurationKey];
        return string.IsNullOrWhiteSpace(configured) ? VoyageEmbeddingGenerator.DefaultBaseAddress : configured;
    }

    private static void ConfigureClient(HttpClient client, string baseAddress, string apiKey)
    {
        client.BaseAddress = new Uri(baseAddress, UriKind.Absolute);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(AuthenticationScheme, apiKey);
    }

    /// <summary>The circuit breaker's sampling window must be at least twice the attempt timeout.</summary>
    private static void ConfigureResilience(HttpStandardResilienceOptions options)
    {
        options.AttemptTimeout.Timeout = AttemptTimeout;
        options.TotalRequestTimeout.Timeout = TotalTimeout;
        options.CircuitBreaker.SamplingDuration = AttemptTimeout * 2;
    }
}
