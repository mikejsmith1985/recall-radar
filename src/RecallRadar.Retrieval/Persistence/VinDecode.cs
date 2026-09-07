// One remembered answer from NHTSA's VIN decoder, so the same descriptor is never asked about twice.
using RecallRadar.Domain.Vehicles;

namespace RecallRadar.Retrieval.Persistence;

/// <summary>
/// What a VIN descriptor and model year decode to.
/// </summary>
/// <remarks>
/// A vehicle's complaints hold about fifty distinct descriptors, and they repeat across model years
/// and across vehicles. Remembering the answer turns a second load into no decode requests at all,
/// and keeps a re-load from leaning on a service that belongs to somebody else.
/// <para>
/// A decode that came back empty is stored too. "vPIC does not know" is an answer, and asking again
/// every load would cost the same requests to learn the same nothing.
/// </para>
/// </remarks>
public sealed class VinDecode
{
    /// <summary>How much of a VIN identifies the version. Mirrors the ingest side's own constant.</summary>
    public const int DescriptorLength = 8;

    public long Id { get; private set; }

    /// <summary>The first eight characters of a VIN, upper-cased.</summary>
    public string Descriptor { get; private set; } = string.Empty;

    public int ModelYear { get; private set; }
    public string? Trim { get; private set; }
    public string? TrimKey { get; private set; }
    public decimal? EngineLitres { get; private set; }
    public int? EngineCylinders { get; private set; }
    public DateTimeOffset DecodedAt { get; private set; }

    /// <summary>The trim and engine this descriptor decoded to.</summary>
    public VehicleFit Fit => VehicleFit.Create(Trim, EngineLitres, EngineCylinders);

    private VinDecode() { }

    /// <summary>Remembers what a descriptor decoded to for one model year.</summary>
    public static VinDecode Create(string descriptor, int modelYear, VehicleFit fit, DateTimeOffset decodedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(descriptor);
        ArgumentNullException.ThrowIfNull(fit);

        return new VinDecode
        {
            Descriptor = descriptor.Trim().ToUpperInvariant(),
            ModelYear = modelYear,
            Trim = fit.Trim,
            TrimKey = fit.TrimKey,
            EngineLitres = fit.EngineLitres,
            EngineCylinders = fit.EngineCylinders,
            DecodedAt = decodedAt,
        };
    }
}
