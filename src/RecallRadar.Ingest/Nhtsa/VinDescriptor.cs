// Reduces a VIN to the part that says which version of a model it is. No network, no state.
namespace RecallRadar.Ingest.Nhtsa;

/// <summary>
/// The first eight characters of a VIN: the world manufacturer identifier and the descriptor
/// section, which is where the series, body style and engine live.
/// </summary>
/// <remarks>
/// Complaints carry an eleven-character VIN with the serial stripped, and those eleven characters
/// include a check digit and a plant code that differ between two identical trucks. Cutting to eight
/// collapses one vehicle's complaints from three hundred distinct strings to about fifty, which is
/// the difference between a decode pass being worth doing and not.
/// </remarks>
public static class VinDescriptor
{
    /// <summary>How much of a VIN identifies the version rather than the individual vehicle.</summary>
    public const int Length = 8;

    /// <summary>The descriptor of a VIN, upper-cased, or null when there is not enough VIN to take one.</summary>
    public static string? From(string? vin)
    {
        if (string.IsNullOrWhiteSpace(vin))
        {
            return null;
        }

        var trimmed = vin.Trim();
        return trimmed.Length < Length ? null : trimmed[..Length].ToUpperInvariant();
    }
}
