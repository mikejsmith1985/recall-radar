// Checks the schema actually constrains the reply the parser and verifier depend on.
using System.Text.Json;
using RecallRadar.Api.Answering;

namespace RecallRadar.Unit.Answering;

public sealed class AnswerSchemaTests
{
    [Fact]
    public void Build_RequiresEveryFieldTheAnswerPathReads()
    {
        var schema = AnswerSchema.Build();

        var required = schema["required"].EnumerateArray().Select(entry => entry.GetString()).ToList();
        Assert.Contains(AnswerSchema.AnswerProperty, required);
        Assert.Contains(AnswerSchema.IsKnownPatternProperty, required);
        Assert.Contains(AnswerSchema.CitationsProperty, required);
        Assert.Contains(AnswerSchema.LinkedCampaignsProperty, required);
    }

    [Fact]
    public void Build_ForbidsFieldsNobodyReads()
    {
        var schema = AnswerSchema.Build();

        Assert.False(schema["additionalProperties"].GetBoolean());
    }

    [Fact]
    public void Build_MakesEveryCitationCarryBothARecordIdAndAQuote()
    {
        var schema = AnswerSchema.Build();

        var citationItems = schema["properties"]
            .GetProperty(AnswerSchema.CitationsProperty)
            .GetProperty("items");
        var required = citationItems.GetProperty("required").EnumerateArray().Select(entry => entry.GetString()).ToList();

        Assert.Equal("array", schema["properties"].GetProperty(AnswerSchema.CitationsProperty).GetProperty("type").GetString());
        Assert.Contains(AnswerSchema.DocumentIdProperty, required);
        Assert.Contains(AnswerSchema.QuoteProperty, required);
        Assert.False(citationItems.GetProperty("additionalProperties").GetBoolean());
    }

    [Fact]
    public void Build_DescribesAQuoteAsVerbatimSoTheModelIsToldTwice()
    {
        // The system prompt says it too. A description in the schema reaches the model at the point
        // it is filling the field, which is where a paraphrase would otherwise creep in.
        var quoteDescription = AnswerSchema.Build()["properties"]
            .GetProperty(AnswerSchema.CitationsProperty)
            .GetProperty("items")
            .GetProperty("properties")
            .GetProperty(AnswerSchema.QuoteProperty)
            .GetProperty("description")
            .GetString();

        Assert.Contains("verbatim", quoteDescription, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("paraphrase", quoteDescription, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Build_ProducesJsonTheApiWouldAccept()
    {
        var serialised = JsonSerializer.Serialize(AnswerSchema.Build());

        using var reparsed = JsonDocument.Parse(serialised);
        Assert.Equal("object", reparsed.RootElement.GetProperty("type").GetString());
    }
}
