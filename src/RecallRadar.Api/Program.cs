// Entry point for the Recall Radar API: configuration, the database, retrieval, and the endpoints.
using RecallRadar.Api.Config;
using RecallRadar.Api.Endpoints;
using RecallRadar.Retrieval.Embeddings;
using RecallRadar.Retrieval.Persistence;
using RecallRadar.Retrieval.Search;

var builder = WebApplication.CreateBuilder(args);

var settings = AppSettings.Load(
    builder.Configuration[AppSettings.ConnectionConfigurationKey],
    Environment.GetEnvironmentVariable);
builder.Services.AddSingleton(settings);
builder.Services.AddDbContext<RecallRadarDbContext>(options =>
    RecallRadarDbContextFactory.Configure(options, settings.ConnectionString));

// Voyage when a key is configured, otherwise a generator that refuses. Refusing is what lets the
// search endpoint answer 409 for the modes needing embeddings while keyword search keeps working.
builder.Services.AddEmbeddingGenerator(builder.Configuration);
builder.Services.AddScoped<HybridSearchService>();

var app = builder.Build();

app.MapHealthEndpoint();
app.MapSearchEndpoints();

app.Run();

/// <summary>Exposed so the integration suite can host the API in-process through WebApplicationFactory.</summary>
public partial class Program;
