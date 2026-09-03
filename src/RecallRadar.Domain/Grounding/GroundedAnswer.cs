// The answer as it may be shown to a user: text plus only the citations that survived verification.
namespace RecallRadar.Domain.Grounding;

/// <summary>
/// What the API returns for a symptom question. Built from a <see cref="CitationCheck"/> so the
/// grounded flag and the dropped count can never disagree with the citations actually listed.
/// </summary>
/// <param name="AnswerText">The model's prose answer.</param>
/// <param name="IsKnownPattern">The model's judgement that the symptom matches a documented pattern. Only meaningful when grounded.</param>
/// <param name="IsGrounded">True only when at least one citation was verified.</param>
/// <param name="VerifiedCitations">Citations with offsets into their record bodies, in the order the model emitted them.</param>
/// <param name="DroppedCitationCount">How many citations were emitted but failed verification.</param>
/// <param name="LinkedCampaigns">Recall campaign numbers the model connected to the symptom.</param>
public sealed record GroundedAnswer(
    string AnswerText,
    bool IsKnownPattern,
    bool IsGrounded,
    IReadOnlyList<VerifiedCitation> VerifiedCitations,
    int DroppedCitationCount,
    IReadOnlyList<string> LinkedCampaigns)
{
    /// <summary>Shown in place of the model's claim when nothing could be verified (FR-010).</summary>
    public const string NotGroundedLabel = "not grounded";

    /// <summary>
    /// Builds the answer from the check. A known-pattern claim with no surviving evidence is downgraded
    /// to false, because a pattern the sources do not support is not known, it is asserted.
    /// </summary>
    public static GroundedAnswer From(string answerText, bool isKnownPattern, CitationCheck check, IReadOnlyList<string> linkedCampaigns)
    {
        ArgumentNullException.ThrowIfNull(answerText);
        ArgumentNullException.ThrowIfNull(check);
        ArgumentNullException.ThrowIfNull(linkedCampaigns);

        return new GroundedAnswer(
            answerText,
            isKnownPattern && check.IsGrounded,
            check.IsGrounded,
            check.Verified,
            check.DroppedCount,
            linkedCampaigns);
    }

    /// <summary>An explicitly ungrounded answer, used when the model refused or returned nothing parseable.</summary>
    public static GroundedAnswer NotGrounded(string explanation) =>
        new(explanation, false, false, [], 0, []);
}
