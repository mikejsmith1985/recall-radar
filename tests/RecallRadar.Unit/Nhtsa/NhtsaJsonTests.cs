// Checks the tolerant JSON readers against the quirks NHTSA's feeds actually have.
using System.Text.Json;
using RecallRadar.Ingest.Nhtsa;

namespace RecallRadar.Unit.Nhtsa;

public sealed class NhtsaJsonTests
{
    private const string Sample = """{"odiNumber": 11760888, "Summary": "text", "crash": false, "vin": null, "results": [1, 2]}""";

    [Fact]
    public void ReadText_AcceptsNumbersStringsAndBooleansAndIgnoresCase()
    {
        using var document = JsonDocument.Parse(Sample);
        var root = document.RootElement;

        Assert.Equal("11760888", NhtsaJson.ReadText(root, "odiNumber"));
        Assert.Equal("text", NhtsaJson.ReadText(root, "summary"));
        Assert.Equal(bool.FalseString, NhtsaJson.ReadText(root, "crash"));
        Assert.Equal(string.Empty, NhtsaJson.ReadText(root, "vin"));
        Assert.Equal(string.Empty, NhtsaJson.ReadText(root, "missing"));
    }

    [Fact]
    public void ReadResults_ReturnsTheArrayOrNothing()
    {
        using var document = JsonDocument.Parse(Sample);
        using var noResults = JsonDocument.Parse("""{"count": 0}""");

        Assert.Equal(2, NhtsaJson.ReadResults(document.RootElement).Count());
        Assert.Empty(NhtsaJson.ReadResults(noResults.RootElement));
    }
}
