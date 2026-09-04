// Entry point for the Recall Radar API: configuration, the database, retrieval, answering, endpoints.
using Anthropic;
using RecallRadar.Api.Answering;
using RecallRadar.Api.Config;
using RecallRadar.Api.Endpoints;
using RecallRadar.Retrieval.Embeddings;
using RecallRadar.Retrieval.Evaluation;
using RecallRadar.Retrieval.Persistence;
using RecallRadar.Retrieval.Search;

var builder = WebApplication.CreateBuilder(args);

var settings = AppSettings.Load(
    builder.Configuration[AppSettings.ConnectionConfigurationKey],
    Environment.GetEnvironmentVariable,
    name => builder.Configuration[name]);
builder.Services.AddSingleton(settings);
builder.Services.AddDbContext<RecallRadarDbContext>(options =>
    RecallRadarDbContextFactory.Configure(options, settings.ConnectionString));

// Voyage when a key is configured, otherwise a generator that refuses. Refusing is what lets the
// search endpoint answer 409 for the modes needing embeddings while keyword search keeps working.
builder.Services.AddEmbeddingGenerator(builder.Configuration);
builder.Services.AddScoped<HybridSearchService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<GroundTruthBuilder>();
builder.Services.AddScoped<EvaluationRunner>();

// Answering is registered only when a key exists, so the ask endpoint can answer 503 by finding no
// service rather than by failing partway through a request that was never going to work.
if (settings.HasAnthropicKey)
{
    builder.Services.AddSingleton(new AnthropicClient(new Anthropic.Core.ClientOptions { ApiKey = settings.AnthropicApiKey! }));
    builder.Services.AddScoped<IAnswerModel, ClaudeAnswerModel>();
    builder.Services.AddScoped<AnswerService>();
}

var app = builder.Build();

app.MapHealthEndpoint();
app.MapSearchEndpoints();
app.MapAskEndpoints();
app.MapEvalEndpoints();

app.Run();

/// <summary>Exposed so the integration suite can host the API in-process through WebApplicationFactory.</summary>
public partial class Program;
