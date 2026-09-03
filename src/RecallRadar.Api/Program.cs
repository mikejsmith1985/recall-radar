// Entry point for the Recall Radar API: wires configuration, the database, and the health endpoint.
using Microsoft.EntityFrameworkCore;
using RecallRadar.Api.Config;
using RecallRadar.Retrieval.Persistence;

var builder = WebApplication.CreateBuilder(args);

var settings = AppSettings.Load(
    builder.Configuration.GetConnectionString("RecallRadar"),
    Environment.GetEnvironmentVariable);
builder.Services.AddSingleton(settings);
builder.Services.AddDbContext<RecallRadarDbContext>(options =>
    RecallRadarDbContextFactory.Configure(options, settings.ConnectionString));
builder.Services.AddHealthChecks().AddDbContextCheck<RecallRadarDbContext>();

var app = builder.Build();

app.MapHealthChecks("/health");

app.Run();

/// <summary>Exposed so the integration suite can host the API in-process through WebApplicationFactory.</summary>
public partial class Program;
