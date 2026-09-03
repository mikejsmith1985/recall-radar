// Checks the recalls client's request shape and response parsing, including day-first dates.
using System.Text.Json;
using RecallRadar.Ingest.Nhtsa;

namespace RecallRadar.Unit.Nhtsa;

public sealed class NhtsaRecallsClientTests
{
    private const string FeedSample = """
        {"Count": 2, "results": [
          {"NHTSACampaignNumber": "19V435000", "ReportReceivedDate": "10/06/2019", "Component": "SUSPENSION:REAR", "Summary": "S", "Consequence": "C", "Remedy": "R"},
          {"NHTSACampaignNumber": "17V472000", "ReportReceivedDate": "25/07/2017", "Component": "SEAT BELTS", "Summary": "S2", "Consequence": "", "Remedy": "R2"}
        ]}
        """;

    [Fact]
    public void BuildRequestUri_UsesTheRecallsPath()
    {
        Assert.Equal(
            "recalls/recallsByVehicle?make=FORD&model=EXPLORER&modelYear=2013",
            NhtsaRecallsClient.BuildRequestUri("FORD", "EXPLORER", 2013).ToString());
    }

    [Fact]
    public void ParseResults_ReadsBothDateOrdersAndAssemblesBodies()
    {
        using var document = JsonDocument.Parse(FeedSample);

        var recalls = NhtsaRecallsClient.ParseResults(document.RootElement);

        Assert.Equal(2, recalls.Count);
        Assert.Equal(new DateOnly(2019, 10, 6), recalls[0].ReportReceivedOn);
        Assert.Equal("S\n\nC\n\nR", recalls[0].BuildBody());
        Assert.Equal(new DateOnly(2017, 7, 25), recalls[1].ReportReceivedOn);
        Assert.Equal("S2\n\nR2", recalls[1].BuildBody());
    }
}
