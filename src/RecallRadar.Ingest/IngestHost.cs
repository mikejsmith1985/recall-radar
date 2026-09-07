// Builds the Generic Host the CLI verbs run inside, and runs the ingest verb to completion.
using System.Diagnostics;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecallRadar.Ingest.Commands;
using RecallRadar.Ingest.Config;
using RecallRadar.Ingest.Nhtsa;
using RecallRadar.Retrieval.Evaluation;
using RecallRadar.Retrieval.Embeddings;
using RecallRadar.Retrieval.Persistence;
using RecallRadar.Retrieval.Search;

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

    /// <summary>Where the evaluation writes its results, relative to the repository root.</summary>
    public const string ResultsDirectoryName = "eval";

    public const string ResultsFileName = "results.json";

    private const string SolutionFileName = "RecallRadar.slnx";
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
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddScoped<HybridSearchService>();
        builder.Services.AddScoped<GroundTruthBuilder>();
        builder.Services.AddScoped<EvaluationRunner>();

        // Voyage when a key is configured, otherwise a generator that refuses. The embed verb asks
        // which one it got before touching the database, so a missing key fails with one clear line.
        builder.Services.AddEmbeddingGenerator(builder.Configuration);
        builder.Services.AddScoped<EmbedCommand>();
        builder.Services.AddScoped<DecodeCommand>();
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

    /// <summary>
    /// Scores retrieval against the derived ground truth and writes the results file. Exit 0 or 1.
    /// </summary>
    /// <remarks>
    /// The results file is written next to the repository rather than only printed, because the
    /// numbers are meant to be committed and compared, not read once and lost to scrollback.
    /// </remarks>
    /// <summary>
    /// Works out which trim and engine each stored complaint belongs to. Exit code 0 or 1.
    /// </summary>
    /// <remarks>
    /// Separate from ingestion for the same reason embedding is: a load that reached three feeds
    /// successfully should not be thrown away because a fourth service was slow. Running it again is
    /// cheap, because every answer is remembered.
    /// </remarks>
    public static async Task<int> RunDecodeAsync(
        IHost host, string? vehicleDisplayName, TextWriter stdout, TextWriter stderr, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        try
        {
            await using var scope = host.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<RecallRadarDbContext>().Database.MigrateAsync(cancellationToken);
            var report = await scope.ServiceProvider.GetRequiredService<DecodeCommand>()
                .DecodeAsync(vehicleDisplayName, cancellationToken);
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

    public static async Task<int> RunEvalAsync(
        IHost host, TextWriter stdout, TextWriter stderr, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(host);
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await using var scope = host.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<RecallRadarDbContext>().Database.MigrateAsync(cancellationToken);
            var result = await scope.ServiceProvider.GetRequiredService<EvaluationRunner>()
                .RunAsync(vehicleId: null, cancellationToken);

            foreach (var line in EvalReport.FormatLines(result, stopwatch.Elapsed))
            {
                await stdout.WriteLineAsync(line);
            }

            var path = await WriteResultsFileAsync(result, cancellationToken);
            await stdout.WriteLineAsync($"written: {path}");
            return SuccessExitCode;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            await stderr.WriteLineAsync($"{ErrorPrefix} {exception.Message}");
            return FailureExitCode;
        }
    }

    /// <summary>Writes the results where they can be committed beside the code that produced them.</summary>
    private static async Task<string> WriteResultsFileAsync(EvaluationResult result, CancellationToken cancellationToken)
    {
        var directory = Path.Combine(FindRepositoryRoot(), ResultsDirectoryName);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, ResultsFileName);
        await File.WriteAllTextAsync(
            path,
            JsonSerializer.Serialize(result, new JsonSerializerOptions(JsonSerializerDefaults.Web) { WriteIndented = true }),
            cancellationToken);
        return path;
    }

    /// <summary>Walks up from the binary to the repository root, identified by the solution file.</summary>
    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, SolutionFileName)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? AppContext.BaseDirectory;
    }

    private static VehicleRegistration FindRegistration(IServiceProvider services, string vehicleDisplayName)
    {
        var options = services.GetRequiredService<IOptions<IngestOptions>>().Value;
        return options.FindByDisplayName(vehicleDisplayName)
            ?? throw new InvalidOperationException(
                $"No registered vehicle is named '{vehicleDisplayName}'. Registered: {string.Join(", ", options.Vehicles.Select(vehicle => $"'{vehicle.DisplayName}'"))}.");
    }
}
