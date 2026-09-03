// Checks the models client's request shape and that model names are de-duplicated.
using System.Text.Json;
using RecallRadar.Ingest.Nhtsa;

namespace RecallRadar.Unit.Nhtsa;

public sealed class NhtsaModelsClientTests
{
    [Fact]
    public void BuildRequestUri_AsksForComplaintIssueModels()
    {
        Assert.Equal(
            "products/vehicle/models?modelYear=2014&make=FORD&issueType=c",
            NhtsaModelsClient.BuildRequestUri("FORD", 2014).ToString());
    }

    [Fact]
    public void ParseModelNames_DeduplicatesCaseInsensitivelyAndSkipsBlanks()
    {
        using var document = JsonDocument.Parse("""
            {"count": 4, "results": [
              {"modelYear": "2014", "make": "FORD", "model": "F-150 SUPER CREW"},
              {"modelYear": "2014", "make": "FORD", "model": "f-150 super crew"},
              {"modelYear": "2014", "make": "FORD", "model": "EXPLORER"},
              {"modelYear": "2014", "make": "FORD", "model": ""}
            ]}
            """);

        var names = NhtsaModelsClient.ParseModelNames(document.RootElement);

        Assert.Equal(2, names.Count);
        Assert.Contains("F-150 SUPER CREW", names);
        Assert.Contains("explorer", names);
    }
}
