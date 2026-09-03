// Builds the Generic Host the CLI verbs run inside, and runs the ingest verb to completion.
using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecallRadar.Ingest.Commands;
using RecallRadar.Ingest.Config;
using RecallRadar.Ingest.Nhtsa;
using RecallRadar.Retrieval.Embeddings;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Ingest;

/// <summary>
/// One composition root for every verb, so the scheduler, the developer and the integration suite
/// exercise the same wiring. The connection string is never a literal: it comes from
/// <c>ConnectionStrings:RecallRadar</c> (the test host's override) or the
/// <c>RECALLRADAR_CONNECTION</c> environment variable the vault or a gitignored <c>.env</c> sets.
/// </summary>
public static class IngestHost
{
    public const string ConnectionConfigurationKey = "ConnectionStrings:RecallRadar";

    /// <summary>
    /// The command's own settings file. Deliberately not called appsettings.json: a project that
    /// references both this and the API would receive two files of that name in one output folder,
    /// and whichever built last would silently win.
    /// </summary>
    public const string SettingsFileName = "ingest.settings.json";
    public const string ErrorPrefix = "error:";
    private const int SuccessExitCode = 0;
    private const int FailureExitCode = 1;

    /// <summary>Creates the host builder with configuration, NHTSA clients, the database and the service registered.</summary>
    /// <remarks>
    /// The content root is pinned to the folder holding the executable rather than the shell's
    /// current directory. A command-line tool is run from wherever the operator happens to be
    /// standing, and its own settings file travels with the binary, not with the caller.
    /// </remarks>
    public static HostApplicationBuilder CreateBuilder(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = AppContext.BaseDirectory,
        });
        builder.Configuration.AddJsonFile(
            Path.Combine(AppContext.BaseDirectory, SettingsFileName), optional: false, reloadOnChange: false);
        builder.Logging.SetMinimumLevel(LogLevel.Warning);
        builder.Services.Configure<IngestOptions>(builder.Configuration.GetSection(IngestOptions.SectionName));
        builder.Services.AddNhtsaClients();
        builder.Services.AddDbContext<RecallRadarDbContext>((provider, options) =>
            RecallRadarDbContextFactory.Configure(options, ResolveConnection(provider.GetRequiredService<IConfiguration>())));
        builder.Services.AddScoped<IngestService>();

        // Voyage when a key is configured, otherwise a generator that refuses. The embed verb asks
        // which one it got before touching the database, so a missing key fails with one clear line.
        builder.Services.AddEmbeddingGenerator(builder.Configuration);
        builder.Services.AddScoped<EmbedCommand>();
        return builder;
    }

    /// <summary>Config override first, then the environment variable; there is no default because a default would embed a password.</summary>
    public static string ResolveConnection(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        var configured = configuration[ConnectionConfigurationKey];
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var fromEnvironment = configuration[RecallRadarDbContextFactory.ConnectionEnvironmentVariable];
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            return fromEnvironment;
        }

        throw new InvalidOperationException(
            $"{RecallRadarDbContextFactory.ConnectionEnvironmentVariable} is not set. Put it in a gitignored .env (see .env.example) or export it in the shell.");
    }

    /// <summary>Runs a load for the named vehicle inside the host, printing the contract output. Exit code 0 or 1.</summary>
    public static async Task<int> RunIngestAsync(IHost host, string vehicleDisplayName, TextWriter stdout, TextWriter stderr, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await using var scope = host.Services.CreateAsyncScope();
            var registration = FindRegistration(scope.ServiceProvider, vehicleDisplayName);
            await scope.ServiceProvider.GetRequiredService<RecallRadarDbContext>().Database.MigrateAsync(cancellationToken);
            var report = await scope.ServiceProvider.GetRequiredService<IngestService>().IngestAsync(registration, cancellationToken);
            foreach (var line in report.FormatLines(stopwatch.Elapsed))
            {
                await stdout.WriteLineAsync(line);
            }

            return SuccessExitCode;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await stderr.WriteLineAsync($"{ErrorPrefix} {exception.Message}");
            return FailureExitCode;
        }
    }

    /// <summary>
    /// Back-fills embeddings inside the host, printing the contract output. Exit code 0 or 1.
    /// </summary>
    /// <remarks>
    /// A missing key surfaces as <c>error: VOYAGE_API_KEY not set</c> through the same catch that
    /// handles every other failure, because to an operator it is one more reason the verb could not
    /// do its job, not a special case needing its own path.
    /// </remarks>
    public static async Task<int> RunEmbedAsync(
        IHost host, string? vehicleDisplayName, TextWriter stdout, TextWriter stderr, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        try
        {
            await using var scope = host.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<RecallRadarDbContext>().Database.MigrateAsync(cancellationToken);
            var report = await scope.ServiceProvider.GetRequiredService<EmbedCommand>()
                .EmbedAsync(vehicleDisplayName, cancellationToken);
            foreach (var line in report.FormatLines())
            {
                await stdout.WriteLineAsync(line);
            }

            return SuccessExitCode;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await stderr.WriteLineAsync($"{ErrorPrefix} {exception.Message}");
            return FailureExitCode;
        }
    }

    private static VehicleRegistration FindRegistration(IServiceProvider services, string vehicleDisplayName)
    {
        var options = services.GetRequiredService<IOptions<IngestOptions>>().Value;
        return options.FindByDisplayName(vehicleDisplayName)
            ?? throw new InvalidOperationException(
                $"No registered vehicle is named '{vehicleDisplayName}'. Registered: {string.Join(", ", options.Vehicles.Select(vehicle => $"'{vehicle.DisplayName}'"))}.");
    }
}
