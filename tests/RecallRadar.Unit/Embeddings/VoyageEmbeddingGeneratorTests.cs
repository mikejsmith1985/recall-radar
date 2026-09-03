// Checks the Voyage adapter's request shape, batching, parsing, failure handling and key hygiene.
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.AI;
using RecallRadar.Retrieval.Embeddings;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Unit.Embeddings;

public sealed class VoyageEmbeddingGeneratorTests
{
    private const string FakeKey = "pa-not-a-real-voyage-key-000000000000";

    [Fact]
    public void BuildRequestBody_NamesTheModelDimensionsAndInputType()
    {
        var body = VoyageEmbeddingGenerator.BuildRequestBody(["first", "second"], VoyageEmbeddingGenerator.DocumentInputType);

        using var parsed = JsonDocument.Parse(body);
        var root = parsed.RootElement;
        Assert.Equal(VoyageEmbeddingGenerator.ModelName, root.GetProperty("model").GetString());
        Assert.Equal(VoyageEmbeddingGenerator.DocumentInputType, root.GetProperty("input_type").GetString());
        Assert.Equal(DocumentChunk.EmbeddingDimensions, root.GetProperty("output_dimension").GetInt32());
        Assert.Equal(["first", "second"], root.GetProperty("input").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact]
    public async Task GenerateAsync_SendsOneRequestPerBatchOfAtMostTheBatchLimit()
    {
        // What is under test is how the inputs are split, so the handler answers with a vector the
        // parser rejects. Parsing 133 full-width vectors would cost a hundred and thirty thousand
        // float conversions to prove something about two request sizes; the round trip itself is
        // covered by the parsing tests below.
        var inputs = Enumerable.Range(0, VoyageEmbeddingGenerator.MaxBatchSize + 5).Select(index => $"chunk {index}").ToList();
        var handler = new RecordingHandler(_ => BuildResponseWithWidth(1));
        using var generator = BuildGenerator(handler);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => generator.GenerateAsync(inputs, cancellationToken: CancellationToken.None));

        Assert.Equal([VoyageEmbeddingGenerator.MaxBatchSize], handler.BatchSizes);
    }

    [Fact]
    public async Task GenerateAsync_SendsTheRemainderAsASecondRequest()
    {
        var inputs = Enumerable.Range(0, VoyageEmbeddingGenerator.MaxBatchSize + 5).Select(index => $"chunk {index}").ToList();
        var handler = new RecordingHandler(batch => BuildEmptyDataResponse(batch.Count));
        using var generator = BuildGenerator(handler);

        var embeddings = await generator.GenerateAsync(inputs, cancellationToken: CancellationToken.None);

        Assert.Empty(embeddings);
        Assert.Equal([VoyageEmbeddingGenerator.MaxBatchSize, 5], handler.BatchSizes);
    }

    [Fact]
    public async Task GenerateAsync_ParsesTheEmbeddingArraysInResponseOrder()
    {
        var handler = new RecordingHandler(_ => BuildResponseWithFirstComponents([0.25f, -0.5f]));
        using var generator = BuildGenerator(handler);

        var embeddings = await generator.GenerateAsync(["one", "two"], cancellationToken: CancellationToken.None);

        Assert.Equal(2, embeddings.Count);
        Assert.Equal(0.25f, embeddings[0].Vector.Span[0]);
        Assert.Equal(-0.5f, embeddings[1].Vector.Span[0]);
    }

    [Fact]
    public async Task GenerateAsync_SendsNothingForAnEmptyBatch()
    {
        var handler = new RecordingHandler(batch => BuildSuccessResponse(batch.Count));
        using var generator = BuildGenerator(handler);

        var embeddings = await generator.GenerateAsync([], cancellationToken: CancellationToken.None);

        Assert.Empty(embeddings);
        Assert.Empty(handler.BatchSizes);
    }

    [Fact]
    public async Task GenerateAsync_UsesTheQueryInputTypeWhenAskedForOne()
    {
        var handler = new RecordingHandler(batch => BuildSuccessResponse(batch.Count));
        using var generator = BuildGenerator(handler);
        var options = VoyageEmbeddingGenerator.ForQuery();

        await generator.GenerateAsync(["exhaust smell"], options, CancellationToken.None);

        Assert.Contains($"\"input_type\":\"{VoyageEmbeddingGenerator.QueryInputType}\"", handler.LastRequestBody, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    public async Task GenerateAsync_TurnsARejectedKeyIntoTheUnavailableExceptionTheApiAlreadyHandles(HttpStatusCode status)
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(status));
        using var generator = BuildGenerator(handler);

        var failure = await Assert.ThrowsAsync<EmbeddingsUnavailableException>(
            () => generator.GenerateAsync(["text"], cancellationToken: CancellationToken.None));

        Assert.Contains(EmbeddingsUnavailableException.ApiKeyVariable, failure.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAsync_NeverPutsTheKeyInAnExceptionEvenWhenTheRequestIsRejected()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent($"{{\"detail\":\"invalid key {FakeKey}\"}}"),
        });
        using var generator = BuildGenerator(handler);

        var failure = await Assert.ThrowsAsync<EmbeddingsUnavailableException>(
            () => generator.GenerateAsync(["text"], cancellationToken: CancellationToken.None));

        Assert.DoesNotContain(FakeKey, failure.ToString(), StringComparison.Ordinal);
        Assert.DoesNotContain(FakeKey, failure.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAsync_ReportsAServerErrorAsAnOutageTheCallerCanFallBackFrom()
    {
        // A raw HttpRequestException here once reached the API as an unhandled failure and became a
        // 500. Keyword search still works while the provider is down, so it is a reportable state.
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        using var generator = BuildGenerator(handler);

        var failure = await Assert.ThrowsAsync<EmbeddingProviderUnavailableException>(
            () => generator.GenerateAsync(["text"], cancellationToken: CancellationToken.None));

        Assert.Contains("500", failure.Message, StringComparison.Ordinal);
        Assert.Contains("mode=sparse", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task GenerateAsync_RejectsAResponseOfTheWrongWidthRatherThanStoreIt()
    {
        var handler = new RecordingHandler(_ => BuildResponseWithWidth(8));
        using var generator = BuildGenerator(handler);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => generator.GenerateAsync(["text"], cancellationToken: CancellationToken.None));

        Assert.Contains(DocumentChunk.EmbeddingDimensions.ToString(), failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ToString_CarriesNoKeyMaterialBecauseTheGeneratorNeverHoldsTheKey()
    {
        var handler = new RecordingHandler(batch => BuildSuccessResponse(batch.Count));
        using var generator = BuildGenerator(handler);

        Assert.DoesNotContain(FakeKey, generator.ToString() ?? string.Empty, StringComparison.Ordinal);
    }

    private static VoyageEmbeddingGenerator BuildGenerator(RecordingHandler handler)
    {
        // The key lives on the client's default headers, set once at registration. The generator
        // never receives it, which is why no code path can put it in a message.
        var client = new HttpClient(handler) { BaseAddress = new Uri(VoyageEmbeddingGenerator.DefaultBaseAddress) };
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", FakeKey);
        return new VoyageEmbeddingGenerator(client);
    }

    /// <summary>
    /// The tail of a full-width vector, rendered once. A batch response repeats the same 1023 zeros
    /// behind each leading component, and formatting them per entry turned a batching assertion into
    /// a hundred thousand float-to-string conversions.
    /// </summary>
    private static readonly string ZeroTail = string.Join(',', Enumerable.Repeat("0", DocumentChunk.EmbeddingDimensions - 1));

    private static HttpResponseMessage BuildSuccessResponse(int count) =>
        BuildResponseWithFirstComponents([.. Enumerable.Repeat(0.1f, count)]);

    private static HttpResponseMessage BuildResponseWithFirstComponents(IReadOnlyList<float> firstComponents)
    {
        var data = firstComponents.Select((component, index) =>
            $"{{\"index\":{index},\"embedding\":[{component.ToString("R")},{ZeroTail}]}}");
        return JsonResponse($"{{\"data\":[{string.Join(',', data)}]}}");
    }

    /// <summary>A well-formed response carrying no vectors, for asserting request shape cheaply.</summary>
    private static HttpResponseMessage BuildEmptyDataResponse(int batchSize)
    {
        _ = batchSize;
        return JsonResponse("{\"data\":[]}");
    }

    private static HttpResponseMessage BuildResponseWithWidth(int width)
    {
        var vector = string.Join(',', Enumerable.Repeat("0.1", width));
        return JsonResponse($"{{\"data\":[{{\"index\":0,\"embedding\":[{vector}]}}]}}");
    }

    private static HttpResponseMessage JsonResponse(string body) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json"),
    };

    /// <summary>Records the batch sizes and bodies the generator sent, and replies from a supplied factory.</summary>
    private sealed class RecordingHandler(Func<IReadOnlyList<string>, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<int> BatchSizes { get; } = [];

        public string LastRequestBody { get; private set; } = string.Empty;

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequestBody = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            var inputs = ReadInputs(LastRequestBody);
            BatchSizes.Add(inputs.Count);
            return respond(inputs);
        }

        private static IReadOnlyList<string> ReadInputs(string body)
        {
            if (string.IsNullOrEmpty(body))
            {
                return [];
            }

            using var parsed = JsonDocument.Parse(body);
            return [.. parsed.RootElement.GetProperty("input").EnumerateArray().Select(item => item.GetString() ?? string.Empty)];
        }
    }
}
