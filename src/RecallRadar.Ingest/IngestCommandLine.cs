// Defines the `ingest`, `embed` and `eval` verbs and parses their options.
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
    public const string EmbedVerb = "embed";
    public const string EvalVerb = "eval";
    public const string VehicleOptionName = "--vehicle";
    private const int NotImplementedExitCode = 1;

    /// <summary>Creates the root command with every verb attached.</summary>
    /// <param name="runIngest">Loads one vehicle's NHTSA records.</param>
    /// <param name="runEmbed">Back-fills embeddings; the vehicle name is optional, so it may be null.</param>
    /// <param name="runEval">Scores retrieval against derived ground truth.</param>
    public static RootCommand Build(
        Func<string, CancellationToken, Task<int>> runIngest,
        Func<string?, CancellationToken, Task<int>> runEmbed,
        Func<CancellationToken, Task<int>> runEval)
    {
        ArgumentNullException.ThrowIfNull(runIngest);
        ArgumentNullException.ThrowIfNull(runEmbed);
        ArgumentNullException.ThrowIfNull(runEval);

        var root = new RootCommand("Recall Radar ingestion, embedding and evaluation.");
        root.Subcommands.Add(BuildIngestCommand(runIngest));
        root.Subcommands.Add(BuildEmbedCommand(runEmbed));
        root.Subcommands.Add(BuildEvalCommand(runEval));
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
            (vehicle, cancellationToken) => RunInHostAsync(
                host => IngestHost.RunIngestAsync(host, vehicle, stdout, stderr, cancellationToken), configureHost),
            (vehicle, cancellationToken) => RunInHostAsync(
                host => IngestHost.RunEmbedAsync(host, vehicle, stdout, stderr, cancellationToken), configureHost),
            _ => RunNotImplementedAsync(stderr, EvalVerb));
        return root.Parse(args).InvokeAsync();
    }

    private static Command BuildIngestCommand(Func<string, CancellationToken, Task<int>> runIngest)
    {
        var vehicleOption = BuildVehicleOption(required: true);
        var command = new Command(IngestVerb, "Fetch NHTSA complaints, recalls and investigations for one vehicle.");
        command.Options.Add(vehicleOption);
        command.SetAction((parseResult, cancellationToken) =>
            runIngest(parseResult.GetRequiredValue(vehicleOption), cancellationToken));
        return command;
    }

    /// <summary>The vehicle is optional here: with none given, every chunk missing a vector is embedded.</summary>
    private static Command BuildEmbedCommand(Func<string?, CancellationToken, Task<int>> runEmbed)
    {
        var vehicleOption = BuildVehicleOption(required: false);
        var command = new Command(EmbedVerb, "Give vectors to stored chunks that have none yet.");
        command.Options.Add(vehicleOption);
        command.SetAction((parseResult, cancellationToken) =>
            runEmbed(parseResult.GetValue(vehicleOption), cancellationToken));
        return command;
    }

    private static Command BuildEvalCommand(Func<CancellationToken, Task<int>> runEval)
    {
        var command = new Command(EvalVerb, "Score dense, sparse and hybrid retrieval against derived ground truth.");
        command.SetAction((_, cancellationToken) => runEval(cancellationToken));
        return command;
    }

    private static Option<string> BuildVehicleOption(bool required) => new(VehicleOptionName)
    {
        Description = "Display name of a configured vehicle, e.g. \"2013 Explorer Sport\".",
        Required = required,
    };

    private static async Task<int> RunInHostAsync(
        Func<IHost, Task<int>> run, Action<HostApplicationBuilder>? configureHost)
    {
        // The verb's own arguments are already parsed; the host gets none, so System.CommandLine
        // options never collide with the host's configuration switches.
        var builder = IngestHost.CreateBuilder([]);
        configureHost?.Invoke(builder);
        using var host = builder.Build();
        return await run(host);
    }

    private static async Task<int> RunNotImplementedAsync(TextWriter stderr, string verb)
    {
        await stderr.WriteLineAsync($"{IngestHost.ErrorPrefix} '{verb}' is not implemented yet.");
        return NotImplementedExitCode;
    }
}
