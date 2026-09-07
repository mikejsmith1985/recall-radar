// Checks the VIN decoder's request shape and what it reads out of a decode response.
using System.Text.Json;
using RecallRadar.Ingest.Nhtsa;

namespace RecallRadar.Unit.Nhtsa;

public sealed class VpicClientTests
{
    /// <summary>Trimmed from the live response for 1FTFW1RJ, the 2023 Raptor R, on 2026-09-07.</summary>
    private const string RaptorSample = """
        {"Count":1,"Results":[{"VIN":"1FTFW1RJ","Make":"FORD","Model":"F-150","ModelYear":"2023",
          "Trim":"SuperCrew-Raptor","DisplacementL":"5.2","EngineCylinders":"8","ErrorCode":"6,12"}]}
        """;

    /// <summary>A real response for a descriptor vPIC has no trim for. The engine still decodes.</summary>
    private const string NoTrimSample = """
        {"Count":1,"Results":[{"VIN":"1FTMF1C5","Trim":"","DisplacementL":"5.0","EngineCylinders":"8","ErrorCode":"6,12"}]}
        """;

    [Fact]
    public void BuildRequestUri_AsksForTheYearBecauseADescriptorDoesNotCarryIt()
    {
        // Position ten holds the model year, and a descriptor stops at eight.
        Assert.Equal(
            "vehicles/DecodeVinValues/1FTFW1RJ?format=json&modelyear=2023",
            VpicClient.BuildRequestUri("1FTFW1RJ", 2023).ToString());
    }

    [Fact]
    public void BuildRequestUri_LeavesTheYearOutWhenThereIsNoneToGive()
    {
        Assert.Equal(
            "vehicles/DecodeVinValues/1FTFW1RJ0PFB00000?format=json",
            VpicClient.BuildRequestUri("1FTFW1RJ0PFB00000", null).ToString());
    }

    [Fact]
    public void ParseFit_ReadsTheTrimAndEngineThatSeparateOneVersionFromAnother()
    {
        using var document = JsonDocument.Parse(RaptorSample);

        var fit = VpicClient.ParseFit(document.RootElement);

        Assert.Equal("SuperCrew-Raptor", fit.Trim);
        Assert.Equal(5.2m, fit.EngineLitres);
        Assert.Equal(8, fit.EngineCylinders);
    }

    [Fact]
    public void ParseFit_IgnoresTheErrorCodeAPartialVinAlwaysCarries()
    {
        // Every partial VIN comes back with an error code because it is incomplete by definition,
        // and the fields are populated anyway. Refusing those would decode nothing at all.
        using var document = JsonDocument.Parse(RaptorSample);

        Assert.True(VpicClient.ParseFit(document.RootElement).IsKnown);
    }

    [Fact]
    public void ParseFit_TreatsABlankFieldAsSomethingNhtsaDoesNotKnow()
    {
        using var document = JsonDocument.Parse(NoTrimSample);

        var fit = VpicClient.ParseFit(document.RootElement);

        Assert.Null(fit.Trim);
        Assert.Equal(5.0m, fit.EngineLitres);
    }

    [Fact]
    public void ParseFit_ReturnsNothingKnownWhenThereAreNoResults()
    {
        using var document = JsonDocument.Parse("""{"Count":0,"Results":[]}""");

        Assert.False(VpicClient.ParseFit(document.RootElement).IsKnown);
    }
}
