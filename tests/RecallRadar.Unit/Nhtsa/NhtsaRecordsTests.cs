// Checks the small behaviours the NHTSA record types carry: titles, bodies and vehicle matching.
using RecallRadar.Ingest.Nhtsa;

namespace RecallRadar.Unit.Nhtsa;

public sealed class NhtsaRecordsTests
{
    [Fact]
    public void Complaint_TitleIsTheOpeningOfTheSummaryOnOneLine()
    {
        var summary = "The contact owns a 2013 Ford Explorer.\nExhaust odor " + new string('x', 100);
        var complaint = new NhtsaComplaint("1", "STRUCTURE", summary, null, "{}");

        var title = complaint.BuildTitle();

        Assert.Equal(NhtsaComplaint.TitleCharacters, title.Length);
        Assert.DoesNotContain('\n', title);
        Assert.StartsWith("The contact owns a 2013 Ford Explorer. Exhaust odor", title);
        Assert.True(complaint.HasSummary);
    }

    [Fact]
    public void Recall_BodyJoinsTheNarrativeSectionsAndSkipsEmptyOnes()
    {
        var recall = new NhtsaRecall("19V435000", "SUSPENSION:REAR", "Summary text.", "", "Remedy text.", null, "{}");

        Assert.Equal("Summary text.\n\nRemedy text.", recall.BuildBody());
        Assert.Equal("19V435000 SUSPENSION:REAR", recall.BuildTitle());
    }

    [Theory]
    [InlineData("FORD", "EXPLORER", 2013, "EXPLORER", 2013, true)]
    [InlineData("FORD", "F-150", 2014, "F-150 SUPER CREW", 2014, true)]
    [InlineData("FORD", "EXPLORER POLICE INTERCEPT", 2013, "EXPLORER", 2013, false)]
    [InlineData("FORD", "EXPLORER", 2014, "EXPLORER", 2013, false)]
    [InlineData("TOYOTA", "EXPLORER", 2013, "EXPLORER", 2013, false)]
    [InlineData("ford", "explorer", 2013, "EXPLORER", 2013, true)]
    public void InvestigationRow_MatchesVehicleByFamilyOrExactModel(
        string rowMake, string rowModel, int rowYear, string configuredModel, int configuredYear, bool isExpectedMatch)
    {
        var row = BuildRow(rowMake, rowModel, rowYear, campaign: "");

        Assert.Equal(isExpectedMatch, row.MatchesVehicle("FORD", configuredModel, configuredYear));
    }

    [Fact]
    public void InvestigationRow_HasCampaignOnlyWhenTheColumnIsFilled()
    {
        Assert.True(BuildRow("FORD", "EXPLORER", 2013, "16V123000").HasCampaign);
        Assert.False(BuildRow("FORD", "EXPLORER", 2013, "  ").HasCampaign);
    }

    private static NhtsaInvestigationRow BuildRow(string make, string model, int year, string campaign) => new(
        "EA17002", make, model, year, "STRUCTURE:BODY", "Ford Motor Company",
        new DateOnly(2017, 7, 27), null, campaign, "Exhaust Odor in Passenger Cab", "Summary.", "raw");
}
