// Entry point for the Recall Radar API: configuration, the database, retrieval, answering, endpoints.
using Anthropic;
using Microsoft.EntityFrameworkCore;
using RecallRadar.Api.Answering;
using RecallRadar.Api.Config;
using RecallRadar.Api.Endpoints;
using RecallRadar.Api.Fixtures;
using RecallRadar.Api.Loading;
using RecallRadar.Ingest;
using RecallRadar.Ingest.Commands;
using RecallRadar.Ingest.Config;
using RecallRadar.Ingest.Nhtsa;
using RecallRadar.Retrieval.Embeddings;
using RecallRadar.Retrieval.Evaluation;
using RecallRadar.Retrieval.Persistence;
using RecallRadar.Retrieval.Search;

var builder = WebApplication.CreateBuilder(args);

// The browser suite runs against this environment: a throwaway seeded database and a scripted
// model, so a run costs nothing, finishes quickly, and asserts the same thing every time.
var isBrowserFixture = builder.Environment.IsEnvironment(UxFixtureEnvironment.Name);

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

// Loading a vehicle now happens here rather than only at a command line. The work runs in the
// background because it reaches three NHTSA feeds and takes minutes; the request that asks for it
// gets a job to watch instead of a timeout.
builder.Services.Configure<IngestOptions>(builder.Configuration.GetSection(IngestOptions.SectionName));
builder.Services.Configure<IngestRunnerOptions>(builder.Configuration.GetSection(IngestRunnerOptions.SectionName));
builder.Services.Configure<ScheduledRefreshOptions>(builder.Configuration.GetSection(ScheduledRefreshOptions.SectionName));
builder.Services.AddNhtsaClients();
builder.Services.AddScoped<IngestService>();
builder.Services.AddScoped<EmbedCommand>();
builder.Services.AddScoped<IngestJobQueue>();

// The browser fixture must not reach NHTSA, so neither background service runs there.
if (!isBrowserFixture)
{
    builder.Services.AddHostedService<IngestJobRunner>();
    builder.Services.AddHostedService<ScheduledRefreshService>();
}

if (isBrowserFixture)
{
    builder.Services.AddScoped<IAnswerModel, ScriptedAnswerModel>();
    builder.Services.AddScoped<AnswerService>();
}
else if (settings.HasAnthropicKey)
{
    // Answering is registered only when a key exists, so the ask endpoint answers 503 by finding no
    // service rather than by failing partway through a request that was never going to work.
    builder.Services.AddSingleton(new AnthropicClient(new Anthropic.Core.ClientOptions { ApiKey = settings.AnthropicApiKey! }));
    builder.Services.AddScoped<IAnswerModel, ClaudeAnswerModel>();
    builder.Services.AddScoped<AnswerService>();
}

var app = builder.Build();

if (isBrowserFixture)
{
    await UxFixtureEnvironment.PrepareAsync(app.Services, app.Lifetime.ApplicationStopping);
}

// The built client is served from the same origin as the API, so a browser test navigates to "/"
// and the page's own /api calls need no proxy or cross-origin allowance.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHealthEndpoint();
app.MapSearchEndpoints();
app.MapVehicleEndpoints();
app.MapAskEndpoints();
app.MapEvalEndpoints();

// Anything that is not an API route is a client route, so the single-page app resolves it. The
// fallback deliberately excludes /api and /health: a mistyped endpoint must answer 404, not hand
// back the page and leave a caller parsing HTML as though it were a result.
app.MapFallback("/api/{*path}", () => Results.NotFound());
app.MapFallbackToFile("{*path:nonfile}", "index.html");

app.Run();

/// <summary>Exposed so the integration suite can host the API in-process through WebApplicationFactory.</summary>
public partial class Program;
