// Checks that the command tree routes each verb to its handler with the parsed options.
using RecallRadar.Ingest;

namespace RecallRadar.Unit;

public sealed class IngestCommandLineTests
{
    [Fact]
    public async Task Ingest_PassesTheVehicleDisplayNameToTheHandler()
    {
        string? received = null;
        var root = IngestCommandLine.Build(
            (vehicle, _) => { received = vehicle; return Task.FromResult(0); },
            _ => Task.FromResult(99));

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
            _ => Task.FromResult(0));

        var parseResult = root.Parse(["ingest"]);

        Assert.NotEmpty(parseResult.Errors);
        Assert.False(wasHandlerCalled);
    }

    [Fact]
    public async Task Eval_RoutesToTheEvalHandler()
    {
        var root = IngestCommandLine.Build(
            (_, _) => Task.FromResult(1),
            _ => Task.FromResult(42));

        var exitCode = await root.Parse(["eval"]).InvokeAsync();

        Assert.Equal(42, exitCode);
    }
}
