// Checks that a vehicle is stored the way NHTSA will be queried for it.
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Unit.Persistence;

public sealed class VehicleTests
{
    [Fact]
    public void Create_UpperCasesNhtsaIdentifiersAndKeepsDisplayNameAsWritten()
    {
        var vehicle = Vehicle.Create(" ford ", "f-150 super crew", 2014, " 2014 F-150 SVT Raptor ");

        Assert.Equal("FORD", vehicle.Make);
        Assert.Equal("F-150 SUPER CREW", vehicle.NhtsaModel);
        Assert.Equal(2014, vehicle.ModelYear);
        Assert.Equal("2014 F-150 SVT Raptor", vehicle.DisplayName);
    }

    [Fact]
    public void Create_RejectsModelYearBeforeNhtsaRecordsExist()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            Vehicle.Create("FORD", "EXPLORER", Vehicle.MinimumModelYear - 1, "Too old"));
    }

    [Theory]
    [InlineData("", "EXPLORER", "2013 Explorer Sport")]
    [InlineData("FORD", " ", "2013 Explorer Sport")]
    [InlineData("FORD", "EXPLORER", "")]
    public void Create_RejectsBlankIdentifiers(string make, string model, string displayName)
    {
        Assert.Throws<ArgumentException>(() => Vehicle.Create(make, model, 2013, displayName));
    }
}
