// Defines the `ingest` and `eval` verbs and parses their options.
using System.CommandLine;

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

    /// <summary>Parses and runs the given arguments against the real handlers.</summary>
    public static Task<int> RunAsync(string[] args)
    {
        var root = Build(NotImplementedIngestAsync, NotImplementedEvalAsync);
        return root.Parse(args).InvokeAsync();
    }

    private static Task<int> NotImplementedIngestAsync(string vehicle, CancellationToken cancellationToken)
    {
        Console.Error.WriteLine($"Ingestion for '{vehicle}' is not implemented yet.");
        return Task.FromResult(1);
    }

    private static Task<int> NotImplementedEvalAsync(CancellationToken cancellationToken)
    {
        Console.Error.WriteLine("Evaluation is not implemented yet.");
        return Task.FromResult(1);
    }
}
