// One evaluation question and the records NHTSA's own links say should be found for it.
namespace RecallRadar.Domain.Evaluation;

/// <summary>
/// The query is an owner complaint's text; the relevant set is the investigation that covered
/// that complaint's component and window plus the recall campaign it produced (FR-012). Nothing
/// here is hand-labelled.
/// </summary>
/// <param name="CaseId">Stable identifier, normally the complaint's ODI number, so runs can be compared case by case.</param>
/// <param name="QueryText">The text submitted to retrieval.</param>
/// <param name="RelevantDocumentIds">Document ids that count as a hit.</param>
public sealed record GroundTruthCase(string CaseId, string QueryText, IReadOnlySet<long> RelevantDocumentIds)
{
    /// <summary>A case with nothing relevant cannot score any retrieval and is excluded from the averages rather than counted as zero.</summary>
    public bool HasRelevantDocuments => RelevantDocumentIds.Count > 0;

    /// <summary>Builds a case, rejecting blank identifiers or queries because they would be unreportable.</summary>
    public static GroundTruthCase Create(string caseId, string queryText, IEnumerable<long> relevantDocumentIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(caseId);
        ArgumentException.ThrowIfNullOrWhiteSpace(queryText);
        ArgumentNullException.ThrowIfNull(relevantDocumentIds);

        return new GroundTruthCase(caseId.Trim(), queryText, relevantDocumentIds.ToHashSet());
    }
}
