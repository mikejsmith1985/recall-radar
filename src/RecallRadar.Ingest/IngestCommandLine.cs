// Defines the `ingest` and `eval` verbs and parses their options.
using System.CommandLine;
using Microsoft.Extensions.Hosting;

namespace RecallRadar.Ingest;

/// <summary>
/// Builds the command tree. Kept separate from Program.cs so the parser can be unit-tested
/// without running anything.
/// </summary>
public static class IngestCommandLine
{
    public const string IngestVerb = "ingest";
    public const string EvalVerb = "eval";
    public const string VehicleOptionName = "--vehicle";
    private const int NotImplementedExitCode = 1;

    /// <summary>Creates the root command with both verbs attached.</summary>
    public static RootCommand Build(Func<string, CancellationToken, Task<int>> runIngest, Func<CancellationToken, Task<int>> runEval)
    {
        ArgumentNullException.ThrowIfNull(runIngest);
        ArgumentNullException.ThrowIfNull(runEval);

        var vehicleOption = new Option<string>(VehicleOptionName)
        {
            Description = "Display name of a configured vehicle, e.g. \"2013 Explorer Sport\".",
            Required = true,
        };

        var ingestCommand = new Command(IngestVerb, "Fetch NHTSA complaints, recalls and investigations for one vehicle.");
        ingestCommand.Options.Add(vehicleOption);
        ingestCommand.SetAction((parseResult, cancellationToken) =>
            runIngest(parseResult.GetRequiredValue(vehicleOption), cancellationToken));

        var evalCommand = new Command(EvalVerb, "Score dense, sparse and hybrid retrieval against derived ground truth.");
        evalCommand.SetAction((_, cancellationToken) => runEval(cancellationToken));

        var root = new RootCommand("Recall Radar ingestion and evaluation.");
        root.Subcommands.Add(ingestCommand);
        root.Subcommands.Add(evalCommand);
        return root;
    }

    /// <summary>Parses and runs the given arguments against the real host, writing to the console.</summary>
    public static Task<int> RunAsync(string[] args) =>
        RunAsync(args, Console.Out, Console.Error, configureHost: null);

    /// <summary>
    /// Parses and runs the arguments, building the host on demand. The optional hook lets the
    /// integration suite redirect configuration (recorded NHTSA server, container database).
    /// </summary>
    public static Task<int> RunAsync(string[] args, TextWriter stdout, TextWriter stderr, Action<HostApplicationBuilder>? configureHost)
    {
        var root = Build(
            (vehicle, cancellationToken) => RunIngestVerbAsync(vehicle, stdout, stderr, configureHost, cancellationToken),
            _ => RunNotImplementedAsync(stderr, EvalVerb));
        return root.Parse(args).InvokeAsync();
    }

    private static async Task<int> RunIngestVerbAsync(
        string vehicle, TextWriter stdout, TextWriter stderr, Action<HostApplicationBuilder>? configureHost, CancellationToken cancellationToken)
    {
        // The verb's own arguments are already parsed; the host gets none, so System.CommandLine
        // options never collide with the host's configuration switches.
        var builder = IngestHost.CreateBuilder([]);
        configureHost?.Invoke(builder);
        using var host = builder.Build();
        return await IngestHost.RunIngestAsync(host, vehicle, stdout, stderr, cancellationToken);
    }

    private static async Task<int> RunNotImplementedAsync(TextWriter stderr, string verb)
    {
        await stderr.WriteLineAsync($"{IngestHost.ErrorPrefix} '{verb}' is not implemented yet.");
        return NotImplementedExitCode;
    }
}
