// Checks a decode pass adds up across vehicles and prints what it did.
using RecallRadar.Ingest.Commands;

namespace RecallRadar.Unit.Commands;

public sealed class DecodeReportTests
{
    [Fact]
    public void AddsOneVehiclesCountsToAnother()
    {
        var total = new DecodeReport(1, 1, 401, 4).Add(new DecodeReport(1, 0, 2130, 101));

        Assert.Equal(new DecodeReport(2, 1, 2531, 105), total);
    }

    [Fact]
    public void StartsFromNothing()
    {
        Assert.Equal(new DecodeReport(1, 1, 401, 4), DecodeReport.Empty.Add(new DecodeReport(1, 1, 401, 4)));
    }

    [Fact]
    public void PrintsBothFactsAnOperatorNeeds()
    {
        // How many were described, and how many can never be narrowed because NHTSA filed no VIN.
        var lines = new DecodeReport(1, 1, 401, 4).FormatLines().ToList();

        Assert.Contains("vehicles: 1, own VINs decoded: 1", lines);
        Assert.Contains("complaints: described 401, no VIN on file 4", lines);
    }

    [Fact]
    public void RefusesToAddNothing()
    {
        Assert.Throws<ArgumentNullException>(() => DecodeReport.Empty.Add(null!));
    }
}
