// Checks the pure rules of a load: de-duplication, body assembly, chunking and link derivation.
using RecallRadar.Ingest;
using RecallRadar.Ingest.Nhtsa;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Unit;

public sealed class IngestPlannerTests
{
    private static readonly DateOnly Opened = new(2017, 7, 27);

    [Fact]
    public void PlanComplaints_CollapsesRepeatedOdiNumbersAndSkipsEmptySummaries()
    {
        var complaints = new[]
        {
            new NhtsaComplaint("11639231", "STRUCTURE", "Exhaust smell in cabin.", new DateOnly(2026, 8, 31), "{a}"),
            new NhtsaComplaint("11639231", "STRUCTURE", "Exhaust smell in cabin.", new DateOnly(2026, 8, 31), "{a}"),
            new NhtsaComplaint(" 11578633 ", "STEERING", "Steering locks up.", null, "{b}"),
            new NhtsaComplaint("11111111", "STEERING", "   ", null, "{c}"),
        };

        var planned = IngestPlanner.PlanComplaints(complaints);

        Assert.Equal(["11639231", "11578633"], planned.Select(document => document.ExternalId));
        Assert.All(planned, document => Assert.Equal(SourceKind.Complaint, document.Kind));
        Assert.All(planned, document => Assert.Single(document.Passages));
        Assert.Equal("Exhaust smell in cabin.", planned[0].Body);
        Assert.Empty(planned[0].Links);
    }

    [Fact]
    public void PlanRecalls_BuildsTheThreeSectionBodyOncePerCampaign()
    {
        var recalls = new[]
        {
            new NhtsaRecall("19V435000", "SUSPENSION:REAR", "Summary.", "Consequence.", "Remedy.", new DateOnly(2019, 10, 6), "{}"),
            new NhtsaRecall("19V435000", "SUSPENSION:REAR", "Summary.", "Consequence.", "Remedy.", new DateOnly(2019, 10, 6), "{}"),
        };

        var planned = IngestPlanner.PlanRecalls(recalls);

        var only = Assert.Single(planned);
        Assert.Equal(SourceKind.Recall, only.Kind);
        Assert.Equal("Summary.\n\nConsequence.\n\nRemedy.", only.Body);
        Assert.Equal("19V435000 SUSPENSION:REAR", only.Title);
        Assert.Equal(new DateOnly(2019, 10, 6), only.FiledOn);
    }

    [Fact]
    public void PlanInvestigations_CollapsesRowsPerActionNumberAndDerivesLinksWithCampaigns()
    {
        var rows = new[]
        {
            BuildRow("EA17002", "ENGINE AND ENGINE COOLING:EXHAUST SYSTEM", campaign: ""),
            BuildRow("EA17002", "STRUCTURE:BODY", campaign: ""),
            BuildRow("PE16008", "POWER TRAIN", campaign: "16V123000"),
            BuildRow("PE16008", "POWER TRAIN", campaign: "16V123000"),
            BuildRow("PE16008", "ENGINE", campaign: "16V123000"),
        };

        var planned = IngestPlanner.PlanInvestigations(rows);

        Assert.Equal(2, planned.Count);
        var exhaust = planned.Single(document => document.ExternalId == "EA17002");
        Assert.Equal("ENGINE AND ENGINE COOLING:EXHAUST SYSTEM; STRUCTURE:BODY", exhaust.Component);
        Assert.Empty(exhaust.Links);
        Assert.Equal(Opened, exhaust.FiledOn);
        var powertrain = planned.Single(document => document.ExternalId == "PE16008");
        Assert.Equal(2, powertrain.Links.Count);
        Assert.All(powertrain.Links, link => Assert.Equal("16V123000", link.CampaignNumber));
        Assert.Equal(["POWER TRAIN", "ENGINE"], powertrain.Links.Select(link => link.Component));
        Assert.Contains("PE16008\tFORD", powertrain.RawPayload);
    }

    [Fact]
    public void PlanInvestigations_SplitsALongSummaryIntoOrderedPassages()
    {
        var longSummary = string.Join("\n\n", Enumerable.Range(0, 5).Select(_ => new string('s', 700)));
        var rows = new[] { BuildRow("EA17002", "STRUCTURE:BODY", campaign: "", summary: longSummary) };

        var planned = IngestPlanner.PlanInvestigations(rows);

        var only = Assert.Single(planned);
        Assert.True(only.Passages.Count > 1);
        Assert.Equal(Enumerable.Range(0, only.Passages.Count), only.Passages.Select(passage => passage.Ordinal));
        Assert.Equal(longSummary, only.Body);
    }

    private static NhtsaInvestigationRow BuildRow(string action, string component, string campaign, string summary = "Summary of the investigation.") => new(
        action, "FORD", "EXPLORER", 2013, component, "Ford Motor Company", Opened, null, campaign, "Subject " + action, summary, $"{action}\tFORD\tEXPLORER");
}
