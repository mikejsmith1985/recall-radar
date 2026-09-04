// Checks that a malformed reply is a reportable failure rather than an exception or a silent gap.
using RecallRadar.Api.Answering;

namespace RecallRadar.Unit.Answering;

public sealed class AnswerResponseParserTests
{
    private const string CompleteReply =
        """
        {
          "answer": "Yes, several owners report the same exhaust odor.",
          "isKnownPattern": true,
          "citations": [
            { "documentId": "42", "quote": "A STRONG EXHAUST ODOR" },
            { "documentId": "7", "quote": "the agency reviewed complaints" }
          ],
          "linkedCampaigns": ["17V001000"]
        }
        """;

    [Fact]
    public void Parse_ReadsEveryFieldOfACompleteReply()
    {
        var result = AnswerResponseParser.Parse(CompleteReply);

        Assert.True(result.IsParsed);
        var answer = result.Answer!;
        Assert.StartsWith("Yes, several owners", answer.AnswerText, StringComparison.Ordinal);
        Assert.True(answer.IsKnownPattern);
        Assert.Equal(2, answer.Citations.Count);
        Assert.Equal("42", answer.Citations[0].DocumentId);
        Assert.Equal("A STRONG EXHAUST ODOR", answer.Citations[0].Quote);
        Assert.Equal(["17V001000"], answer.LinkedCampaigns);
    }

    [Fact]
    public void Parse_KeepsCitationOrderSoTheAnswerAndItsEvidenceStayAligned()
    {
        var result = AnswerResponseParser.Parse(CompleteReply);

        Assert.Equal(["42", "7"], result.Answer!.Citations.Select(citation => citation.DocumentId));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("[1, 2, 3]")]
    public void Parse_ReportsMalformedInputInsteadOfThrowing(string? json)
    {
        var result = AnswerResponseParser.Parse(json);

        Assert.False(result.IsParsed);
        Assert.Equal(AnswerResponseParser.MalformedJsonReason, result.Failure);
    }

    [Theory]
    [InlineData("""{"isKnownPattern": true}""")]
    [InlineData("""{"answer": ""}""")]
    [InlineData("""{"answer": "   "}""")]
    [InlineData("""{"answer": 42}""")]
    public void Parse_TreatsAMissingOrEmptyAnswerAsAFailure(string json)
    {
        var result = AnswerResponseParser.Parse(json);

        Assert.False(result.IsParsed);
        Assert.Equal(AnswerResponseParser.MissingAnswerReason, result.Failure);
    }

    [Fact]
    public void Parse_AcceptsAnAnswerWithNoCitations()
    {
        // The prompt allows an honest "the records do not show that", which arrives exactly this way.
        var result = AnswerResponseParser.Parse("""{"answer": "These records do not show that.", "isKnownPattern": false}""");

        Assert.True(result.IsParsed);
        Assert.Empty(result.Answer!.Citations);
        Assert.Empty(result.Answer.LinkedCampaigns);
        Assert.False(result.Answer.IsKnownPattern);
    }

    [Fact]
    public void Parse_SkipsMalformedCitationEntriesRatherThanFailingTheWholeReply()
    {
        const string mixed =
            """
            {"answer": "Yes.", "citations": ["not an object", {"documentId": "42", "quote": "real"}, {"quote": "no id"}]}
            """;

        var result = AnswerResponseParser.Parse(mixed);

        Assert.True(result.IsParsed);
        Assert.Equal(2, result.Answer!.Citations.Count);
        Assert.Equal("42", result.Answer.Citations[0].DocumentId);
        // A citation with no id survives parsing and is dropped by the verifier, where the reason is recorded.
        Assert.Equal(string.Empty, result.Answer.Citations[1].DocumentId);
    }

    [Fact]
    public void Parse_IgnoresNonStringCampaignEntries()
    {
        var result = AnswerResponseParser.Parse("""{"answer": "Yes.", "linkedCampaigns": ["17V001000", 42, "", null]}""");

        Assert.Equal(["17V001000"], result.Answer!.LinkedCampaigns);
    }

    [Theory]
    [InlineData("RECORD 1886", "1886")]
    [InlineData("record 1886", "1886")]
    [InlineData("  RECORD  1886  ", "1886")]
    [InlineData("1886", "1886")]
    public void Parse_StripsTheRecordLabelTheModelEchoesFromThePrompt(string emitted, string expected)
    {
        // A live run lost every citation this way: the prompt heads records "RECORD 1886", and the
        // model used the whole label as the id. The quote check is untouched; only the label is.
        var json = $$"""{"answer": "Yes.", "citations": [{"documentId": {{System.Text.Json.JsonSerializer.Serialize(emitted)}}, "quote": "q"}]}""";

        var result = AnswerResponseParser.Parse(json);

        Assert.Equal(expected, result.Answer!.Citations[0].DocumentId);
    }

    [Fact]
    public void NormaliseDocumentId_LeavesAnIdThatMerelyContainsTheWordAlone()
    {
        Assert.Equal("RECORDING-7", AnswerResponseParser.NormaliseDocumentId("RECORDING-7"));
    }
}
