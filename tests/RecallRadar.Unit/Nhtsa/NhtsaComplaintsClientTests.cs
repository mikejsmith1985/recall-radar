// Checks the complaints client's request shape and response parsing without any network.
using System.Text.Json;
using RecallRadar.Ingest.Nhtsa;

namespace RecallRadar.Unit.Nhtsa;

public sealed class NhtsaComplaintsClientTests
{
    private const string FeedSample = """
        {"count": 2, "message": "Results returned successfully", "results": [
          {"odiNumber": 11639231, "components": "ENGINE AND ENGINE COOLING", "summary": "Exhaust smell in cabin.", "dateComplaintFiled": "08/31/2026", "products": []},
          {"odiNumber": "11578633", "components": "STEERING", "summary": "", "dateComplaintFiled": "bad"}
        ]}
        """;

    [Fact]
    public void BuildRequestUri_EscapesTheModelAndKeepsTheYear()
    {
        var uri = NhtsaComplaintsClient.BuildRequestUri("ford", "F-150 SUPER CREW", 2014);

        Assert.False(uri.IsAbsoluteUri);
        Assert.Equal("complaints/complaintsByVehicle?make=ford&model=F-150%20SUPER%20CREW&modelYear=2014", uri.ToString());
    }

    [Fact]
    public void ParseResults_MapsNumericAndStringIdentifiersAndKeepsRawJson()
    {
        using var document = JsonDocument.Parse(FeedSample);

        var complaints = NhtsaComplaintsClient.ParseResults(document.RootElement);

        Assert.Equal(2, complaints.Count);
        Assert.Equal("11639231", complaints[0].OdiNumber);
        Assert.Equal(new DateOnly(2026, 8, 31), complaints[0].FiledOn);
        Assert.Contains("Exhaust smell", complaints[0].RawJson);
        Assert.Equal("11578633", complaints[1].OdiNumber);
        Assert.Null(complaints[1].FiledOn);
        Assert.False(complaints[1].HasSummary);
    }

    [Fact]
    public void ParseResults_EmptyFeedGivesEmptyList()
    {
        using var document = JsonDocument.Parse("""{"count": 0, "results": []}""");

        Assert.Empty(NhtsaComplaintsClient.ParseResults(document.RootElement));
    }
}
