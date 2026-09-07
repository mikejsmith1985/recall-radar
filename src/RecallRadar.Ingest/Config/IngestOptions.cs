// Configuration for ingestion: which vehicles to load and where NHTSA's read-only feeds live.
namespace RecallRadar.Ingest.Config;

/// <summary>
/// A vehicle as the owner registered it, with the exact model strings NHTSA accepts.
/// </summary>
/// <remarks>
/// NHTSA's two feeds do not share a model vocabulary. Complaints are filed against a body style
/// ("F-150 SUPER CREW"); recalls are issued against the base model ("F-150") and answer a body
/// style with 400 Bad Request. Where one name serves both, as with EXPLORER, only
/// <see cref="NhtsaModel"/> needs setting.
/// </remarks>
public sealed class VehicleRegistration
{
    public string Make { get; init; } = string.Empty;

    /// <summary>Model name the complaints feed accepts, and the name the vehicle is stored under.</summary>
    public string NhtsaModel { get; init; } = string.Empty;

    /// <summary>Model name the recalls feed accepts. Defaults to <see cref="NhtsaModel"/> when unset.</summary>
    public string? RecallModel { get; init; }

    public int ModelYear { get; init; }
    public string DisplayName { get; init; } = string.Empty;

    /// <summary>
    /// The owner's VIN, when they gave one. NHTSA files every version of a model under one name, so
    /// this is the only thing that says which version this vehicle actually is.
    /// </summary>
    public string? Vin { get; init; }

    /// <summary>The model name to send to the recalls feed.</summary>
    public string ResolveRecallModel() => string.IsNullOrWhiteSpace(RecallModel) ? NhtsaModel : RecallModel.Trim();

    /// <summary>Throws a readable error for a registration that could never be looked up.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Make) || string.IsNullOrWhiteSpace(NhtsaModel) || string.IsNullOrWhiteSpace(DisplayName))
        {
            throw new InvalidOperationException($"Vehicle '{DisplayName}' needs Make, NhtsaModel and DisplayName.");
        }

        if (ModelYear <= 0)
        {
            throw new InvalidOperationException($"Vehicle '{DisplayName}' needs a ModelYear.");
        }
    }
}

/// <summary>
/// Bound from the <c>RecallRadar</c> configuration section. The base addresses are overridable so
/// the integration suite can point every client at a recorded server instead of the live feeds.
/// </summary>
public sealed class IngestOptions
{
    public const string SectionName = "RecallRadar";
    public const string DefaultNhtsaApiBaseUrl = "https://api.nhtsa.gov/";
    public const string DefaultInvestigationsFlatFileUrl = "https://static.nhtsa.gov/odi/ffdd/inv/FLAT_INV.zip";

    /// <summary>vPIC is NHTSA's product catalogue and the only decoder of VINs. A separate service.</summary>
    public const string DefaultVpicApiBaseUrl = "https://vpic.nhtsa.dot.gov/api/";

    public string NhtsaApiBaseUrl { get; init; } = DefaultNhtsaApiBaseUrl;
    public string InvestigationsFlatFileUrl { get; init; } = DefaultInvestigationsFlatFileUrl;
    public string VpicApiBaseUrl { get; init; } = DefaultVpicApiBaseUrl;
    public List<VehicleRegistration> Vehicles { get; init; } = [];

    /// <summary>Finds a registered vehicle by the name the owner uses for it, ignoring case and padding.</summary>
    public VehicleRegistration? FindByDisplayName(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        return Vehicles.FirstOrDefault(vehicle =>
            string.Equals(vehicle.DisplayName.Trim(), displayName.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
