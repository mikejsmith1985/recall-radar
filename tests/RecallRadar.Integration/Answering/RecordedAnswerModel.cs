// A model that replays a recorded reply, so the answer pipeline is tested without spending money.
using RecallRadar.Api.Answering;

namespace RecallRadar.Integration.Answering;

/// <summary>
/// Stands in for Claude. It records what it was shown, so a test can assert that only retrieved
/// records reached the prompt, and returns a reply the test chose, so verification can be driven
/// with a true quote, a fabricated one, or a refusal.
/// </summary>
public sealed class RecordedAnswerModel(ModelReply reply) : IAnswerModel
{
    /// <summary>The records the service passed to the model on the last call.</summary>
    public IReadOnlyList<PromptRecord> LastRecords { get; private set; } = [];

    /// <summary>The user message the service built, so a test can check what the model could see.</summary>
    public string LastUserMessage { get; private set; } = string.Empty;

    public int CallCount { get; private set; }

    public Task<ModelReply> AskAsync(
        string vehicleName, string question, IReadOnlyList<PromptRecord> records, CancellationToken cancellationToken)
    {
        CallCount++;
        LastRecords = records;
        LastUserMessage = AnswerPrompt.BuildUserMessage(vehicleName, question, records);
        return Task.FromResult(reply);
    }

    /// <summary>Builds a reply carrying the given citations, in the schema's shape.</summary>
    public static ModelReply BuildReply(
        string answerText, bool isKnownPattern, IEnumerable<(string DocumentId, string Quote)> citations,
        IEnumerable<string>? campaigns = null)
    {
        var citationJson = string.Join(",", citations.Select(citation =>
            $$"""{"documentId": {{Quote(citation.DocumentId)}}, "quote": {{Quote(citation.Quote)}}}"""));
        var campaignJson = string.Join(",", (campaigns ?? []).Select(Quote));

        return ModelReply.Answered(
            $$"""
            {"answer": {{Quote(answerText)}}, "isKnownPattern": {{(isKnownPattern ? "true" : "false")}},
             "citations": [{{citationJson}}], "linkedCampaigns": [{{campaignJson}}]}
            """);
    }

    private static string Quote(string value) => System.Text.Json.JsonSerializer.Serialize(value);
}
