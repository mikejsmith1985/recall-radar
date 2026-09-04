// Asks Claude the question, with the schema that makes the reply checkable.
using System.Text.Json;
using Anthropic;
using Anthropic.Models.Messages;

namespace RecallRadar.Api.Answering;

/// <summary>
/// The live model. Structured output constrains the reply to the schema, so the parser reads data
/// rather than guessing at prose. Server-side refusal fallbacks are deliberately not enabled: a
/// question about a safety defect that the model declines should surface as a refusal, not be
/// quietly re-routed to a different model (research R5).
/// </summary>
public sealed class ClaudeAnswerModel(AnthropicClient client) : IAnswerModel
{
    /// <summary>The model this application is written against.</summary>
    public const string ModelId = "claude-opus-5";

    /// <summary>Answers run to roughly 1,500 tokens; this leaves room without inviting an essay.</summary>
    public const int MaxTokens = 4096;

    public const string EmptyReplyReason = "The model returned no answer text.";
    public const string RefusalReason = "The model declined to answer this question.";
    public const string OutageReason = "The answering model could not be reached. Try again shortly.";

    public async Task<ModelReply> AskAsync(
        string vehicleName, string question, IReadOnlyList<PromptRecord> records, CancellationToken cancellationToken)
    {
        var userMessage = AnswerPrompt.BuildUserMessage(vehicleName, question, records);
        try
        {
            var response = await client.Messages.Create(
                new MessageCreateParams
                {
                    Model = ModelId,
                    MaxTokens = MaxTokens,
                    System = AnswerPrompt.SystemPrompt,
                    Messages = [new MessageParam { Role = Role.User, Content = userMessage }],
                    OutputConfig = new OutputConfig { Format = new JsonOutputFormat { Schema = AnswerSchema.Build() } },
                },
                cancellationToken: cancellationToken);

            return ReadReply(response);
        }
        catch (Exception failure) when (failure is not OperationCanceledException)
        {
            // A model outage must not reach the reader as a server fault: the endpoint reports it as
            // an ungrounded answer with the reason, which is what a reader can act on.
            return ModelReply.Declined(OutageReason);
        }
    }

    /// <summary>Reads the reply, checking the stop reason before trusting any content.</summary>
    private static ModelReply ReadReply(Message response)
    {
        if (response.StopReason == StopReason.Refusal)
        {
            return ModelReply.Declined(DescribeRefusal(response));
        }

        var text = response.Content
            .Select(ReadText)
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        return string.IsNullOrWhiteSpace(text) ? ModelReply.Declined(EmptyReplyReason) : ModelReply.Answered(text);
    }

    /// <summary>Names the refusal category when the API supplies one, so a decline is explainable.</summary>
    private static string DescribeRefusal(Message response)
    {
        var category = response.StopDetails?.Category?.ToString();
        return string.IsNullOrWhiteSpace(category) ? RefusalReason : $"{RefusalReason} Category: {category}.";
    }

    /// <summary>Reads a text block's content, ignoring thinking and any other block kind.</summary>
    private static string? ReadText(ContentBlock block) => block.Value is TextBlock textBlock ? textBlock.Text : null;
}
