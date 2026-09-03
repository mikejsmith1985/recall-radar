// Runs every citation in an answer through the verifier and keeps the accounting of what survived.
namespace RecallRadar.Domain.Grounding;

/// <summary>A citation that failed verification, kept with its reason so the failure is visible, not hidden.</summary>
public sealed record DroppedCitation(Citation Citation, string Reason);

/// <summary>
/// The result of checking an answer's citations against the records that were actually supplied
/// to the model. Fails closed: a citation is dropped unless every check passes, and an answer is
/// grounded only when at least one citation survives.
/// </summary>
public sealed record CitationCheck(
    IReadOnlyList<VerifiedCitation> Verified,
    IReadOnlyList<DroppedCitation> Dropped)
{
    public const string UnknownDocumentReason = "The citation names a record that was not among those supplied to the model.";
    public const string MissingDocumentIdReason = "The citation names no record at all.";

    public int VerifiedCount => Verified.Count;
    public int DroppedCount => Dropped.Count;
    public int EmittedCount => VerifiedCount + DroppedCount;

    /// <summary>Grounded means evidence survived. Zero survivors is not a weaker answer; it is no answer.</summary>
    public bool IsGrounded => VerifiedCount > 0;

    /// <summary>
    /// Checks each citation in the order the model emitted it. Verified citations keep that order so the
    /// answer text and its citation list stay aligned.
    /// </summary>
    /// <param name="citations">The citations the model emitted.</param>
    /// <param name="bodiesById">The verbatim body of every record the model was shown, keyed by the id it was shown under.</param>
    public static CitationCheck Run(IReadOnlyList<Citation> citations, IReadOnlyDictionary<string, string> bodiesById)
    {
        ArgumentNullException.ThrowIfNull(citations);
        ArgumentNullException.ThrowIfNull(bodiesById);

        var verified = new List<VerifiedCitation>();
        var dropped = new List<DroppedCitation>();
        foreach (var citation in citations)
        {
            var failure = FirstFailure(citation, bodiesById, out var verification);
            if (failure is null)
            {
                verified.Add(new VerifiedCitation(citation, verification.StartOffset, verification.EndOffset));
            }
            else
            {
                dropped.Add(new DroppedCitation(citation, failure));
            }
        }

        return new CitationCheck(verified, dropped);
    }

    /// <summary>Returns the first reason the citation must be dropped, or null when it passes every check.</summary>
    private static string? FirstFailure(
        Citation citation, IReadOnlyDictionary<string, string> bodiesById, out QuoteVerification verification)
    {
        verification = QuoteVerification.Failed(string.Empty);
        if (!citation.HasDocumentId)
        {
            return MissingDocumentIdReason;
        }

        if (!bodiesById.TryGetValue(citation.DocumentId, out var body))
        {
            return UnknownDocumentReason;
        }

        verification = QuoteVerifier.Verify(citation.Quote, body);
        return verification.IsVerified ? null : verification.Reason;
    }
}
