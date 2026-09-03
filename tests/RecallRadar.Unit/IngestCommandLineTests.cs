// Checks that the command tree routes each verb to its handler with the parsed options.
using RecallRadar.Ingest;

namespace RecallRadar.Unit;

public sealed class IngestCommandLineTests
{
    private const int UnusedHandlerExitCode = 99;

    [Fact]
    public async Task Ingest_PassesTheVehicleDisplayNameToTheHandler()
    {
        string? received = null;
        var root = IngestCommandLine.Build(
            (vehicle, _) => { received = vehicle; return Task.FromResult(0); },
            (_, _) => Task.FromResult(UnusedHandlerExitCode),
            _ => Task.FromResult(UnusedHandlerExitCode));

        var exitCode = await root.Parse(["ingest", "--vehicle", "2013 Explorer Sport"]).InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.Equal("2013 Explorer Sport", received);
    }

    [Fact]
    public void Ingest_WithoutVehicleIsAParseErrorNotAHandlerCall()
    {
        var wasHandlerCalled = false;
        var root = IngestCommandLine.Build(
            (_, _) => { wasHandlerCalled = true; return Task.FromResult(0); },
            (_, _) => Task.FromResult(UnusedHandlerExitCode),
            _ => Task.FromResult(UnusedHandlerExitCode));

        var parseResult = root.Parse(["ingest"]);

        Assert.NotEmpty(parseResult.Errors);
        Assert.False(wasHandlerCalled);
    }

    [Fact]
    public async Task Embed_PassesTheVehicleDisplayNameWhenOneIsGiven()
    {
        string? received = null;
        var root = BuildWithEmbedHandler(vehicle => received = vehicle);

        var exitCode = await root.Parse(["embed", "--vehicle", "2014 F-150 SVT Raptor"]).InvokeAsync();

        Assert.Equal(0, exitCode);
        Assert.Equal("2014 F-150 SVT Raptor", received);
    }

    [Fact]
    public async Task Embed_RunsWithoutAVehicleBecauseBackFillingEverythingIsTheCommonCase()
    {
        var wasHandlerCalled = false;
        string? received = "not null yet";
        var root = BuildWithEmbedHandler(vehicle => { wasHandlerCalled = true; received = vehicle; });

        var parseResult = root.Parse(["embed"]);
        var exitCode = await parseResult.InvokeAsync();

        Assert.Empty(parseResult.Errors);
        Assert.Equal(0, exitCode);
        Assert.True(wasHandlerCalled);
        Assert.Null(received);
    }

    [Fact]
    public async Task Eval_RoutesToTheEvalHandler()
    {
        var root = IngestCommandLine.Build(
            (_, _) => Task.FromResult(1),
            (_, _) => Task.FromResult(1),
            _ => Task.FromResult(42));

        var exitCode = await root.Parse(["eval"]).InvokeAsync();

        Assert.Equal(42, exitCode);
    }

    private static System.CommandLine.RootCommand BuildWithEmbedHandler(Action<string?> onEmbed) =>
        IngestCommandLine.Build(
            (_, _) => Task.FromResult(UnusedHandlerExitCode),
            (vehicle, _) => { onEmbed(vehicle); return Task.FromResult(0); },
            _ => Task.FromResult(UnusedHandlerExitCode));
}
