// The ground-truth join: which recall campaign an NHTSA investigation resulted in.
namespace RecallRadar.Retrieval.Persistence;

/// <summary>
/// Links an investigation document to the recall campaign number NHTSA recorded for it. The
/// evaluation harness treats the investigation and that recall as the relevant documents for
/// complaints about the same vehicle and component filed while the investigation was open.
/// </summary>
public sealed class InvestigationLink
{
    public long Id { get; private set; }
    public long InvestigationDocumentId { get; private set; }
    public SourceDocument? InvestigationDocument { get; private set; }
    public string CampaignNumber { get; private set; } = string.Empty;
    public string Component { get; private set; } = string.Empty;
    public DateOnly OpenedOn { get; private set; }
    public DateOnly? ClosedOn { get; private set; }

    private InvestigationLink() { }

    /// <summary>Creates a link. A closed-on date earlier than the opened-on date is a parse error, not data.</summary>
    public static InvestigationLink Create(
        long investigationDocumentId, string campaignNumber, string component, DateOnly openedOn, DateOnly? closedOn)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(campaignNumber);
        if (closedOn is { } closed && closed < openedOn)
        {
            throw new ArgumentException("An investigation cannot close before it opened.", nameof(closedOn));
        }

        return new InvestigationLink
        {
            InvestigationDocumentId = investigationDocumentId,
            CampaignNumber = campaignNumber.Trim(),
            Component = component?.Trim() ?? string.Empty,
            OpenedOn = openedOn,
            ClosedOn = closedOn,
        };
    }

    /// <summary>Whether a complaint filed on the given date falls inside the investigation window.</summary>
    public bool CoversDate(DateOnly filedOn) => filedOn >= OpenedOn && (ClosedOn is null || filedOn <= ClosedOn);
}
