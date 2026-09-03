// Turns text into vectors using Voyage AI, the provider Anthropic recommends for embeddings.
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Retrieval.Embeddings;

/// <summary>
/// The production embedding provider, behind the framework's
/// <see cref="IEmbeddingGenerator{TInput, TEmbedding}"/> seam.
/// </summary>
/// <remarks>
/// Drift justification (Article VII): Voyage publishes no C# SDK and no
/// <c>Microsoft.Extensions.AI</c> provider package, so this HTTP adapter is the minimum custom
/// piece. Everything around it is framework: the interface is Microsoft's, retries and timeouts
/// come from the standard resilience pipeline on the injected client, and nothing here retries by
/// hand.
///
/// The API key is deliberately absent from this class. It is attached to the client's default
/// headers once, at registration, so no field, message or log statement here can carry it
/// (Article IX).
/// </remarks>
public sealed class VoyageEmbeddingGenerator(HttpClient httpClient) : IEmbeddingGenerator<string, Embedding<float>>
{
    /// <summary>The model whose output width matches the stored column.</summary>
    public const string ModelName = "voyage-3.5";

    /// <summary>Voyage accepts at most this many inputs per request.</summary>
    public const int MaxBatchSize = 128;

    public const string DefaultBaseAddress = "https://api.voyageai.com/v1/";
    public const string EmbeddingsPath = "embeddings";

    /// <summary>Voyage embeds stored passages and search queries differently; these name the two.</summary>
    public const string DocumentInputType = "document";
    public const string QueryInputType = "query";

    /// <summary>Key under which the input type travels on <see cref="EmbeddingGenerationOptions"/>.</summary>
    public const string InputTypeOptionKey = "input_type";

    private const string DataProperty = "data";
    private const string EmbeddingProperty = "embedding";

    /// <summary>Options that embed a search query rather than a stored passage.</summary>
    public static EmbeddingGenerationOptions ForQuery() => BuildOptions(QueryInputType);

    /// <summary>Options that embed a stored passage. This is the default when no options are given.</summary>
    public static EmbeddingGenerationOptions ForDocument() => BuildOptions(DocumentInputType);

    /// <summary>Embeds every input, one request per batch, preserving input order across batches.</summary>
    public async Task<GeneratedEmbeddings<Embedding<float>>> GenerateAsync(
        IEnumerable<string> values,
        EmbeddingGenerationOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(values);
        var inputType = ReadInputType(options);
        var embeddings = new GeneratedEmbeddings<Embedding<float>>();

        foreach (var batch in values.Chunk(MaxBatchSize))
        {
            foreach (var embedding in await SendBatchAsync(batch, inputType, cancellationToken))
            {
                embeddings.Add(embedding);
            }
        }

        return embeddings;
    }

    /// <summary>Renders the request body Voyage expects for one batch.</summary>
    public static string BuildRequestBody(IReadOnlyList<string> inputs, string inputType)
    {
        ArgumentNullException.ThrowIfNull(inputs);
        var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            writer.WriteString("model", ModelName);
            writer.WriteString(InputTypeOptionKey, inputType);
            writer.WriteNumber("output_dimension", DocumentChunk.EmbeddingDimensions);
            writer.WriteStartArray("input");
            foreach (var input in inputs)
            {
                writer.WriteStringValue(input);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    /// <summary>Answers for its own type through the framework's service-discovery seam.</summary>
    public object? GetService(Type serviceType, object? serviceKey = null)
    {
        ArgumentNullException.ThrowIfNull(serviceType);
        return serviceKey is null && serviceType.IsInstanceOfType(this) ? this : null;
    }

    public void Dispose()
    {
        // The client is owned by IHttpClientFactory, which pools and disposes its handlers.
    }

    private async Task<IReadOnlyList<Embedding<float>>> SendBatchAsync(
        IReadOnlyList<string> batch, string inputType, CancellationToken cancellationToken)
    {
        using var content = new StringContent(BuildRequestBody(batch, inputType), Encoding.UTF8, "application/json");
        HttpResponseMessage response;
        try
        {
            response = await httpClient.PostAsync(EmbeddingsPath, content, cancellationToken);
        }
        catch (Exception failure) when (failure is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            // A provider that is down is not a configuration problem, and it must not surface as an
            // unhandled error: keyword search still works, so the caller is told to fall back.
            throw EmbeddingProviderUnavailableException.FromTransportFailure(failure);
        }

        using (response)
        {
            EnsureKeyWasAccepted(response.StatusCode);
            if (!response.IsSuccessStatusCode)
            {
                throw EmbeddingProviderUnavailableException.FromStatus((int)response.StatusCode);
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
            return ParseEmbeddings(document.RootElement);
        }
    }

    /// <summary>
    /// A rejected key means the same thing to a caller as no key at all, so it takes the path the
    /// API already renders as a 409. The response body is never quoted: it can echo the key back.
    /// </summary>
    private static void EnsureKeyWasAccepted(HttpStatusCode status)
    {
        if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new EmbeddingsUnavailableException(
                $"Voyage rejected the credential ({(int)status}). Check {EmbeddingsUnavailableException.ApiKeyVariable} in the vault.");
        }
    }

    /// <summary>Reads the <c>data</c> array, rejecting any vector that is not the stored width.</summary>
    private static IReadOnlyList<Embedding<float>> ParseEmbeddings(JsonElement root)
    {
        if (!root.TryGetProperty(DataProperty, out var data) || data.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"The Voyage response carried no '{DataProperty}' array.");
        }

        return [.. data.EnumerateArray().Select(ParseEmbedding)];
    }

    private static Embedding<float> ParseEmbedding(JsonElement element)
    {
        if (!element.TryGetProperty(EmbeddingProperty, out var vector) || vector.ValueKind != JsonValueKind.Array)
        {
            throw new InvalidOperationException($"A Voyage result carried no '{EmbeddingProperty}' array.");
        }

        var components = vector.EnumerateArray().Select(component => component.GetSingle()).ToArray();
        if (components.Length != DocumentChunk.EmbeddingDimensions)
        {
            throw new InvalidOperationException(
                $"Voyage returned {components.Length} dimensions but the column stores {DocumentChunk.EmbeddingDimensions}.");
        }

        return new Embedding<float>(components);
    }

    private static EmbeddingGenerationOptions BuildOptions(string inputType) => new()
    {
        ModelId = ModelName,
        Dimensions = DocumentChunk.EmbeddingDimensions,
        AdditionalProperties = new AdditionalPropertiesDictionary { [InputTypeOptionKey] = inputType },
    };

    /// <summary>Stored passages are the common case, so anything unspecified embeds as a document.</summary>
    private static string ReadInputType(EmbeddingGenerationOptions? options) =>
        options?.AdditionalProperties?.TryGetValue(InputTypeOptionKey, out var value) == true && value is string inputType
            ? inputType
            : DocumentInputType;
}
