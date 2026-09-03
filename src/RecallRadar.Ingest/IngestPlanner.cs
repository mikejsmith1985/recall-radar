// Pure mapping from NHTSA records to the documents, passages and links that will be stored.
using RecallRadar.Domain.Records;
using RecallRadar.Ingest.Nhtsa;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Ingest;

/// <summary>A recall campaign an investigation led to, as recorded on one flat-file row.</summary>
public sealed record PlannedLink(string CampaignNumber, string Component, DateOnly OpenedOn, DateOnly? ClosedOn);

/// <summary>Everything needed to store one record, decided before any database work starts.</summary>
public sealed record PlannedDocument(
    SourceKind Kind,
    string ExternalId,
    string Component,
    DateOnly? FiledOn,
    string Title,
    string Body,
    string RawPayload,
    IReadOnlyList<RetrievablePassage> Passages,
    IReadOnlyList<PlannedLink> Links);

/// <summary>
/// Decides what gets stored. No I/O, so every rule here (de-duplication, body assembly, chunking,
/// link derivation) is unit-tested in isolation from the network and the database.
/// </summary>
public static class IngestPlanner
{
    private const string ComponentSeparator = "; ";

    /// <summary>
    /// One document per ODI number. The F-150's body-style model names return the same complaints,
    /// and NHTSA occasionally repeats a record, so the ODI number is the identity, not the row.
    /// Complaints with no summary are skipped: a record with no text can never be cited.
    /// </summary>
    public static IReadOnlyList<PlannedDocument> PlanComplaints(IEnumerable<NhtsaComplaint> complaints)
    {
        ArgumentNullException.ThrowIfNull(complaints);
        return complaints
            .Where(complaint => complaint.HasSummary && !string.IsNullOrWhiteSpace(complaint.OdiNumber))
            .DistinctBy(complaint => complaint.OdiNumber.Trim())
            .Select(complaint => new PlannedDocument(
                SourceKind.Complaint,
                complaint.OdiNumber.Trim(),
                complaint.Components,
                complaint.FiledOn,
                complaint.BuildTitle(),
                complaint.Summary,
                complaint.RawJson,
                PassageChunker.AsSinglePassage(complaint.Summary),
                Links: []))
            .ToList();
    }

    /// <summary>One document per campaign number; the body is the three narrative sections.</summary>
    public static IReadOnlyList<PlannedDocument> PlanRecalls(IEnumerable<NhtsaRecall> recalls)
    {
        ArgumentNullException.ThrowIfNull(recalls);
        return recalls
            .Where(recall => !string.IsNullOrWhiteSpace(recall.CampaignNumber) && !string.IsNullOrWhiteSpace(recall.BuildBody()))
            .DistinctBy(recall => recall.CampaignNumber.Trim())
            .Select(recall => new PlannedDocument(
                SourceKind.Recall,
                recall.CampaignNumber.Trim(),
                recall.Component,
                recall.ReportReceivedOn,
                recall.BuildTitle(),
                recall.BuildBody(),
                recall.RawJson,
                PassageChunker.AsSinglePassage(recall.BuildBody()),
                Links: []))
            .ToList();
    }

    /// <summary>
    /// The flat file has one row per investigation × component; they collapse to one document per
    /// action number whose links carry every distinct (campaign, component) pair with a campaign.
    /// </summary>
    public static IReadOnlyList<PlannedDocument> PlanInvestigations(IEnumerable<NhtsaInvestigationRow> rows)
    {
        ArgumentNullException.ThrowIfNull(rows);
        return rows
            .Where(row => !string.IsNullOrWhiteSpace(row.ActionNumber) && !string.IsNullOrWhiteSpace(row.Summary))
            .GroupBy(row => row.ActionNumber.Trim())
            .Select(group => PlanInvestigation(group.Key, group.ToList()))
            .ToList();
    }

    private static PlannedDocument PlanInvestigation(string actionNumber, IReadOnlyList<NhtsaInvestigationRow> rows)
    {
        var first = rows[0];
        var components = rows.Select(row => row.Component).Where(component => component.Length > 0).Distinct().ToList();
        var links = rows
            .Where(row => row.HasCampaign && row.OpenedOn is not null)
            .Select(row => new PlannedLink(row.CampaignNumber.Trim(), row.Component, row.OpenedOn!.Value, row.ClosedOn))
            .DistinctBy(link => (link.CampaignNumber, link.Component))
            .ToList();

        return new PlannedDocument(
            SourceKind.Investigation,
            actionNumber,
            string.Join(ComponentSeparator, components),
            first.OpenedOn,
            first.Subject,
            first.Summary,
            string.Join("\n", rows.Select(row => row.RawLine)),
            PassageChunker.SplitIntoPassages(first.Summary),
            links);
    }
}
