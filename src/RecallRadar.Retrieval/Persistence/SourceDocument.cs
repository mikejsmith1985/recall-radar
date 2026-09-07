// A single NHTSA record (complaint, recall or investigation) stored verbatim so quotes can be checked against it.
using RecallRadar.Domain.Vehicles;

namespace RecallRadar.Retrieval.Persistence;

/// <summary>The three NHTSA record types Recall Radar retrieves from.</summary>
public enum SourceKind
{
    Complaint = 1,
    Recall = 2,
    Investigation = 3,
}

/// <summary>
/// One NHTSA record. <see cref="Body"/> is stored exactly as received, because the quote
/// verifier tests a citation against this text and nothing else (Article X).
/// </summary>
public sealed class SourceDocument
{
    public long Id { get; private set; }
    public SourceKind Kind { get; private set; }

    /// <summary>NHTSA's own identifier: ODI number, campaign number, or investigation action number.</summary>
    public string ExternalId { get; private set; } = string.Empty;

    public int VehicleId { get; private set; }
    public Vehicle? Vehicle { get; private set; }
    public string Component { get; private set; } = string.Empty;
    public DateOnly? FiledOn { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Body { get; private set; } = string.Empty;

    /// <summary>The untouched JSON or flat-file row, kept so a re-parse never needs the network.</summary>
    public string RawPayload { get; private set; } = string.Empty;

    /// <summary>The first eight VIN characters this record was filed under, when it carried a VIN.</summary>
    /// <remarks>
    /// Kept alongside the decoded fields so a decode can be repeated or corrected later without
    /// re-reading every payload, and so a record with no VIN is distinguishable from one whose VIN
    /// nothing has decoded yet.
    /// </remarks>
    public string? VinDescriptor { get; private set; }

    public string? Trim { get; private set; }

    /// <summary>The trim normalised for comparison. Written whenever the trim is.</summary>
    public string? TrimKey { get; private set; }
    public decimal? EngineLitres { get; private set; }
    public int? EngineCylinders { get; private set; }

    /// <summary>The trim and engine this record belongs to, as far as anything is known.</summary>
    public VehicleFit Fit => VehicleFit.Create(Trim, EngineLitres, EngineCylinders);

    /// <summary>Records which version of the model this document was filed under.</summary>
    /// <remarks>
    /// Separate from creation because the descriptor is read at load time and decoded afterwards,
    /// in one pass over the fifty-odd distinct descriptors rather than once per record.
    /// </remarks>
    public void DescribeFit(string? vinDescriptor, VehicleFit fit)
    {
        ArgumentNullException.ThrowIfNull(fit);
        VinDescriptor = string.IsNullOrWhiteSpace(vinDescriptor) ? null : vinDescriptor.Trim().ToUpperInvariant();
        Trim = fit.Trim;
        TrimKey = fit.TrimKey;
        EngineLitres = fit.EngineLitres;
        EngineCylinders = fit.EngineCylinders;
    }

    public ICollection<DocumentChunk> Chunks { get; private set; } = new List<DocumentChunk>();

    private SourceDocument() { }

    /// <summary>Creates a record. The body is required because a record with no text can never be cited.</summary>
    public static SourceDocument Create(
        SourceKind kind,
        string externalId,
        int vehicleId,
        string component,
        DateOnly? filedOn,
        string title,
        string body,
        string rawPayload)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(externalId);
        ArgumentException.ThrowIfNullOrWhiteSpace(body);

        return new SourceDocument
        {
            Kind = kind,
            ExternalId = externalId.Trim(),
            VehicleId = vehicleId,
            Component = component?.Trim() ?? string.Empty,
            FiledOn = filedOn,
            Title = title?.Trim() ?? string.Empty,
            Body = body,
            RawPayload = rawPayload ?? string.Empty,
        };
    }
}
