// Checks vehicle registration validation and lookup by display name.
using RecallRadar.Ingest.Config;

namespace RecallRadar.Unit.Config;

public sealed class IngestOptionsTests
{
    private static readonly IngestOptions Options = new()
    {
        Vehicles =
        [
            new VehicleRegistration { Make = "FORD", NhtsaModel = "EXPLORER", ModelYear = 2013, DisplayName = "2013 Explorer Sport" },
            new VehicleRegistration { Make = "FORD", NhtsaModel = "F-150 SUPER CREW", ModelYear = 2014, DisplayName = "2014 F-150 SVT Raptor" },
        ],
    };

    [Fact]
    public void FindByDisplayName_IgnoresCaseAndPadding()
    {
        var found = Options.FindByDisplayName("  2014 f-150 svt raptor ");

        Assert.NotNull(found);
        Assert.Equal("F-150 SUPER CREW", found.NhtsaModel);
        Assert.Null(Options.FindByDisplayName("2015 Mustang"));
    }

    [Fact]
    public void Defaults_PointAtTheLiveNhtsaFeeds()
    {
        var defaults = new IngestOptions();

        Assert.StartsWith("https://api.nhtsa.gov/", defaults.NhtsaApiBaseUrl);
        Assert.EndsWith("FLAT_INV.zip", defaults.InvestigationsFlatFileUrl);
        Assert.Empty(defaults.Vehicles);
    }

    [Fact]
    public void Validate_RejectsIncompleteRegistrations()
    {
        var missingModel = new VehicleRegistration { Make = "FORD", ModelYear = 2013, DisplayName = "x" };
        var missingYear = new VehicleRegistration { Make = "FORD", NhtsaModel = "EXPLORER", DisplayName = "x" };

        Assert.Throws<InvalidOperationException>(missingModel.Validate);
        Assert.Throws<InvalidOperationException>(missingYear.Validate);
        Options.Vehicles[0].Validate();
    }

    [Fact]
    public void ResolveRecallModel_FallsBackToTheComplaintModelWhenUnset()
    {
        var sharedName = new VehicleRegistration { Make = "FORD", NhtsaModel = "EXPLORER", ModelYear = 2013, DisplayName = "2013 Explorer Sport" };

        Assert.Equal("EXPLORER", sharedName.ResolveRecallModel());
    }

    [Fact]
    public void ResolveRecallModel_UsesTheBaseModelWhenTheFeedsDisagree()
    {
        // The complaints feed wants a body style; the recalls feed answers one with 400 Bad Request.
        var splitNames = new VehicleRegistration
        {
            Make = "FORD",
            NhtsaModel = "F-150 SUPER CREW",
            RecallModel = " F-150 ",
            ModelYear = 2014,
            DisplayName = "2014 F-150 SVT Raptor",
        };

        Assert.Equal("F-150", splitNames.ResolveRecallModel());
        Assert.Equal("F-150 SUPER CREW", splitNames.NhtsaModel);
    }
}
