// Checks what the model is actually shown, which decides what it can honestly cite.
using RecallRadar.Api.Answering;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Unit.Answering;

public sealed class AnswerPromptTests
{
    private static readonly PromptRecord Complaint = new(
        "42", SourceKind.Complaint, "11257832", new DateOnly(2016, 5, 1),
        "A STRONG EXHAUST ODOR ENTERS THE CABIN WHEN ACCELERATING.");

    private static readonly PromptRecord Investigation = new(
        "7", SourceKind.Investigation, "EA17002", new DateOnly(2017, 7, 27),
        "During the EA17-002 investigation, the agency reviewed complaints.");

    private static readonly PromptRecord Undated = new(
        "9", SourceKind.Recall, "19V435000", null, "Ford is recalling certain vehicles.");

    [Fact]
    public void SystemPrompt_TellsTheModelItsQuotesAreCheckedCharacterForCharacter()
    {
        Assert.Contains("character for character", AnswerPrompt.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("discarded", AnswerPrompt.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("paraphrase", AnswerPrompt.SystemPrompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SystemPrompt_AllowsAnHonestNoAnswer()
    {
        // Without this the model is pushed to manufacture evidence when the records hold none.
        Assert.Contains("do not answer the question", AnswerPrompt.SystemPrompt, StringComparison.Ordinal);
        Assert.Contains("cite nothing", AnswerPrompt.SystemPrompt, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildUserMessage_CarriesEveryRecordIdAndBody()
    {
        var message = AnswerPrompt.BuildUserMessage("2013 Explorer Sport", "exhaust smell?", [Complaint, Investigation]);

        Assert.Contains("RECORD 42", message, StringComparison.Ordinal);
        Assert.Contains("RECORD 7", message, StringComparison.Ordinal);
        Assert.Contains(Complaint.Body, message, StringComparison.Ordinal);
        Assert.Contains(Investigation.Body, message, StringComparison.Ordinal);
        Assert.Contains("2013 Explorer Sport", message, StringComparison.Ordinal);
        Assert.Contains("Question: exhaust smell?", message, StringComparison.Ordinal);
    }

    [Fact]
    public void BuildUserMessage_OrdersRecordsByIdSoRepeatedQuestionsShareAPrefix()
    {
        var oneOrder = AnswerPrompt.BuildUserMessage("v", "q", [Complaint, Investigation, Undated]);
        var anotherOrder = AnswerPrompt.BuildUserMessage("v", "q", [Undated, Complaint, Investigation]);

        Assert.Equal(oneOrder, anotherOrder);
        Assert.True(oneOrder.IndexOf("RECORD 7", StringComparison.Ordinal) < oneOrder.IndexOf("RECORD 42", StringComparison.Ordinal));
    }

    [Fact]
    public void BuildUserMessage_SaysUnknownRatherThanGuessingAMissingDate()
    {
        var message = AnswerPrompt.BuildUserMessage("v", "q", [Undated]);

        Assert.Contains("filed: unknown", message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("", "question")]
    [InlineData("vehicle", "")]
    [InlineData("vehicle", "   ")]
    public void BuildUserMessage_RejectsAnEmptyVehicleOrQuestion(string vehicle, string question)
    {
        Assert.Throws<ArgumentException>(() => AnswerPrompt.BuildUserMessage(vehicle, question, [Complaint]));
    }

    [Fact]
    public void BuildBodyIndex_KeysEveryBodyByTheIdTheModelWasShown()
    {
        var index = AnswerPrompt.BuildBodyIndex([Complaint, Investigation]);

        Assert.Equal(Complaint.Body, index["42"]);
        Assert.Equal(Investigation.Body, index["7"]);
        Assert.False(index.ContainsKey("99"));
    }

    [Fact]
    public void OrderForStablePrompt_PutsNonNumericIdsLastWithoutThrowing()
    {
        var odd = Complaint with { DocumentId = "not-a-number" };

        var ordered = AnswerPrompt.OrderForStablePrompt([odd, Investigation]);

        Assert.Equal("7", ordered[0].DocumentId);
        Assert.Equal("not-a-number", ordered[1].DocumentId);
    }
}
