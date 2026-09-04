// Derives the evaluation's ground truth from NHTSA's own records rather than from hand labels.
using Microsoft.EntityFrameworkCore;
using RecallRadar.Domain.Evaluation;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Retrieval.Evaluation;

/// <summary>
/// Builds the (query, relevant-set) pairs the evaluation scores against.
/// </summary>
/// <remarks>
/// Nothing here is labelled by hand. NHTSA opens an investigation, records which component it
/// concerns and when, and names the recall campaign it produced. So for each investigation: the
/// relevant documents are the investigation itself plus the recalls for its campaign, and the
/// queries are the complaints about the same component filed while it was open. Those are the
/// complaints the investigation was actually about, which is exactly what good retrieval should
/// surface. Deriving it this way means the ground truth cannot drift from the data, and nobody
/// has to be trusted to have labelled it fairly.
/// </remarks>
public sealed class GroundTruthBuilder(RecallRadarDbContext database)
{
    /// <summary>Complaints per investigation. Enough to measure, few enough that one busy investigation cannot dominate.</summary>
    public const int MaximumQueriesPerInvestigation = 20;

    /// <summary>
    /// How many characters of the component string must agree. NHTSA writes components at several
    /// granularities for the same fault, so matching the whole string would find almost nothing.
    /// </summary>
    public const int ComponentPrefixLength = 12;

    /// <summary>Builds every case for one vehicle, or for all vehicles when none is named.</summary>
    public async Task<IReadOnlyList<GroundTruthCase>> BuildAsync(int? vehicleId, CancellationToken cancellationToken)
    {
        var links = await LoadLinksAsync(vehicleId, cancellationToken);
        if (links.Count == 0)
        {
            return [];
        }

        var cases = new List<GroundTruthCase>();
        foreach (var link in links)
        {
            cases.AddRange(await BuildCasesForAsync(link, cancellationToken));
        }

        return cases;
    }

    /// <summary>An investigation with no campaign proves nothing, so it is not a case.</summary>
    private async Task<IReadOnlyList<InvestigationLinkRow>> LoadLinksAsync(int? vehicleId, CancellationToken cancellationToken)
    {
        // The vehicle filter is applied in C# rather than as a nullable comparison inside the
        // expression tree, which EF Core cannot translate alongside the join below.
        var links = database.InvestigationLinks.AsNoTracking().Where(link => link.CampaignNumber != "");
        if (vehicleId is { } scopedVehicleId)
        {
            links = links.Where(link => link.InvestigationDocument!.VehicleId == scopedVehicleId);
        }

        // Ordered before the projection: EF Core cannot sort by the fields of a record it has not
        // built yet. The order is what makes two runs produce identical cases.
        return await links
            .OrderBy(link => link.InvestigationDocument!.ExternalId).ThenBy(link => link.Component)
            .Select(link => new InvestigationLinkRow(
                link.InvestigationDocumentId,
                link.InvestigationDocument!.VehicleId,
                link.InvestigationDocument.ExternalId,
                link.CampaignNumber,
                link.Component,
                link.OpenedOn,
                link.ClosedOn))
            .ToListAsync(cancellationToken);
    }

    private async Task<IReadOnlyList<GroundTruthCase>> BuildCasesForAsync(
        InvestigationLinkRow link, CancellationToken cancellationToken)
    {
        var relevantIds = await FindRelevantDocumentsAsync(link, cancellationToken);
        var queries = await FindQueryComplaintsAsync(link, cancellationToken);

        return
        [
            .. queries.Select(complaint => GroundTruthCase.Create(
                $"{link.InvestigationExternalId}:{complaint.ExternalId}", complaint.Body, relevantIds)),
        ];
    }

    /// <summary>The investigation itself, plus every recall for the campaign it produced.</summary>
    private async Task<IReadOnlyList<long>> FindRelevantDocumentsAsync(
        InvestigationLinkRow link, CancellationToken cancellationToken)
    {
        var recallIds = await database.SourceDocuments.AsNoTracking()
            .Where(document => document.VehicleId == link.VehicleId
                && document.Kind == SourceKind.Recall
                && document.ExternalId == link.CampaignNumber)
            .Select(document => document.Id)
            .ToListAsync(cancellationToken);

        return [link.InvestigationDocumentId, .. recallIds];
    }

    /// <summary>
    /// Complaints about the same component filed while the investigation was open. An open
    /// investigation has no close date, so everything after it opened counts.
    /// </summary>
    private async Task<IReadOnlyList<ComplaintRow>> FindQueryComplaintsAsync(
        InvestigationLinkRow link, CancellationToken cancellationToken)
    {
        var componentPrefix = ComponentPrefixOf(link.Component);
        if (componentPrefix.Length == 0)
        {
            return [];
        }

        return await database.SourceDocuments.AsNoTracking()
            .Where(document => document.VehicleId == link.VehicleId
                && document.Kind == SourceKind.Complaint
                && document.Component.StartsWith(componentPrefix)
                && document.FiledOn != null
                && document.FiledOn >= link.OpenedOn
                && (link.ClosedOn == null || document.FiledOn <= link.ClosedOn))
            .OrderBy(document => document.ExternalId)
            .Select(document => new ComplaintRow(document.Id, document.ExternalId, document.Body))
            .Take(MaximumQueriesPerInvestigation)
            .ToListAsync(cancellationToken);
    }

    /// <summary>The leading part of a component string, used to match across NHTSA's granularities.</summary>
    public static string ComponentPrefixOf(string? component)
    {
        var trimmed = component?.Trim() ?? string.Empty;
        return trimmed.Length <= ComponentPrefixLength ? trimmed : trimmed[..ComponentPrefixLength];
    }

    private sealed record InvestigationLinkRow(
        long InvestigationDocumentId,
        int VehicleId,
        string InvestigationExternalId,
        string CampaignNumber,
        string Component,
        DateOnly OpenedOn,
        DateOnly? ClosedOn);

    private sealed record ComplaintRow(long Id, string ExternalId, string Body);
}
