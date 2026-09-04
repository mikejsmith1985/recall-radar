// The seam between "what we ask" and "who answers", so the pipeline is testable without spending money.
namespace RecallRadar.Api.Answering;

/// <summary>What came back from the model: either JSON to parse, or the reason there is none.</summary>
/// <param name="Json">The structured-output text, when the model answered.</param>
/// <param name="Refusal">Why there is no answer: a safety refusal, an outage, or an empty reply.</param>
public sealed record ModelReply(string? Json, string? Refusal)
{
    public static ModelReply Answered(string json) => new(json, null);

    public static ModelReply Declined(string reason) => new(null, reason);
}

/// <summary>
/// Asking a model a question, narrowed to what this application needs. Narrow on purpose: the
/// service under test can be driven with a recorded reply, and a live call is never made by
/// accident in a test run.
/// </summary>
public interface IAnswerModel
{
    /// <summary>Asks the question about one vehicle, showing only the supplied records.</summary>
    Task<ModelReply> AskAsync(
        string vehicleName, string question, IReadOnlyList<PromptRecord> records, CancellationToken cancellationToken);
}
