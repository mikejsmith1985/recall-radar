// A citation as the model emitted it: which record it names and the passage it claims to quote.
namespace RecallRadar.Domain.Grounding;

/// <summary>
/// One claimed citation. It is a claim, not evidence, until <see cref="QuoteVerifier"/> finds the
/// quote in the record body. Kept as a record so tests and JSON parsing can build it directly.
/// </summary>
/// <param name="DocumentId">The identifier of the source record the model cited, exactly as it was given to the model.</param>
/// <param name="Quote">The passage the model says appears in that record.</param>
public sealed record Citation(string DocumentId, string Quote)
{
    /// <summary>True when the citation names a record; a blank id can never be looked up.</summary>
    public bool HasDocumentId => !string.IsNullOrWhiteSpace(DocumentId);

    /// <summary>True when there is any quoted text at all; an empty quote is a citation with nothing to check.</summary>
    public bool HasQuote => !string.IsNullOrWhiteSpace(Quote);
}
