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

    [Theory]
    [InlineData("""{"Count":0,"Message":"Results returned successfully","results":[]}""")]
    [InlineData("""{"count":0,"results":[]}""")]
    public void IsEmptyResultEnvelope_RecognisesNhtsaSayingNothingWasFound(string body)
    {
        // NHTSA sends this with status 400, so a car too new to have been recalled is
        // indistinguishable from a malformed request by status code alone.
        Assert.True(NhtsaRecallsClient.IsEmptyResultEnvelope(body));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<html><body>Bad Gateway</body></html>")]
    [InlineData("""{"Count":1,"results":[{"NHTSACampaignNumber":"19V435000"}]}""")]
    [InlineData("""[1, 2, 3]""")]
    // An error page that happens to be JSON has no results array at all. Absent is not empty:
    // treating the two alike turned a stub's 404 into a successful load with zero recalls.
    [InlineData("""{"Status":"No matching mapping found","Error":"..."}""")]
    [InlineData("""{"Count":0,"results":null}""")]
    [InlineData("""{"Count":0}""")]
    public void IsEmptyResultEnvelope_LeavesEveryOtherFailureFailing(string? body)
    {
        // A real problem wearing the same status code must still fail the load.
        Assert.False(NhtsaRecallsClient.IsEmptyResultEnvelope(body));
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
