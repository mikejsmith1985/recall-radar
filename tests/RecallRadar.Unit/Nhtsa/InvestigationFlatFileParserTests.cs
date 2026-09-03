// Checks the flat-file row parser and its vehicle filter against realistic rows.
using RecallRadar.Ingest.Config;
using RecallRadar.Ingest.Nhtsa;

namespace RecallRadar.Unit.Nhtsa;

public sealed class InvestigationFlatFileParserTests
{
    private const string ExplorerRow =
        "EA17002\tFORD\tEXPLORER\t2013\tENGINE AND ENGINE COOLING:EXHAUST SYSTEM\tFord Motor Company\t20170727\t20230117\t\tExhaust Odor in Passenger Cab\tDuring the EA17-002 investigation, the agency reviewed reports.";
    private const string RaptorFamilyRow =
        "PE16003\tFORD\tF-150\t2014\tPOWER TRAIN\tFord Motor Company\t20160301\t\t16V123000\tDownshift\tSummary.";
    private const string OtherMakeRow =
        "PE99999\tTOYOTA\tTUNDRA\t2014\tPOWER TRAIN\tToyota\t20160301\t\t\tSubject\tSummary.";
    private const string ShortRow = "PE00000\tFORD\tEXPLORER";

    private static readonly VehicleRegistration Explorer = new() { Make = "FORD", NhtsaModel = "EXPLORER", ModelYear = 2013, DisplayName = "2013 Explorer Sport" };
    private static readonly VehicleRegistration Raptor = new() { Make = "FORD", NhtsaModel = "F-150 SUPER CREW", ModelYear = 2014, DisplayName = "2014 F-150 SVT Raptor" };

    [Fact]
    public void ParseLine_ReadsAllElevenColumns()
    {
        var row = InvestigationFlatFileParser.ParseLine(ExplorerRow);

        Assert.NotNull(row);
        Assert.Equal("EA17002", row.ActionNumber);
        Assert.Equal("EXPLORER", row.Model);
        Assert.Equal(2013, row.ModelYear);
        Assert.Equal("ENGINE AND ENGINE COOLING:EXHAUST SYSTEM", row.Component);
        Assert.Equal(new DateOnly(2017, 7, 27), row.OpenedOn);
        Assert.Equal(new DateOnly(2023, 1, 17), row.ClosedOn);
        Assert.False(row.HasCampaign);
        Assert.Equal("Exhaust Odor in Passenger Cab", row.Subject);
        Assert.StartsWith("During the EA17-002", row.Summary);
        Assert.Equal(ExplorerRow, row.RawLine);
    }

    [Fact]
    public void ParseLine_ReturnsNullForShortOrBlankLines()
    {
        Assert.Null(InvestigationFlatFileParser.ParseLine(ShortRow));
        Assert.Null(InvestigationFlatFileParser.ParseLine(""));
    }

    [Fact]
    public void ParseLines_KeepsOnlyRowsForConfiguredVehiclesIncludingFamilyModels()
    {
        var rows = InvestigationFlatFileParser.ParseLines([ExplorerRow, RaptorFamilyRow, OtherMakeRow, ShortRow], [Explorer, Raptor]);

        Assert.Equal(["EA17002", "PE16003"], rows.Select(row => row.ActionNumber));
        Assert.Equal("16V123000", rows[1].CampaignNumber);
    }

    [Fact]
    public void ParseLines_WithOnlyTheExplorerDropsTheTruck()
    {
        var rows = InvestigationFlatFileParser.ParseLines([ExplorerRow, RaptorFamilyRow], [Explorer]);

        Assert.Single(rows);
        Assert.Equal("EA17002", rows[0].ActionNumber);
    }
}
