// What a VIN is, so the form, the API and the decoder all agree on it.
namespace RecallRadar.Domain.Vehicles;

/// <summary>
/// The shape of a vehicle identification number.
/// </summary>
/// <remarks>
/// Seventeen characters since 1981, and never containing I, O or Q, because those are too easily
/// read as 1 and 0. Checked in one place so the form, the endpoint and the decoder cannot disagree
/// about what counts as one.
/// </remarks>
public static class VehicleVin
{
    /// <summary>
    /// A VIN identifying no vehicle, for tests, placeholders and documentation.
    /// </summary>
    /// <remarks>
    /// The first eight characters are a real Ford descriptor, because that is what says "F-150,
    /// SuperCrew Raptor, 5.2 litre V8" and it identifies a model rather than a truck. Everything
    /// after them is zeroes: a real VIN names somebody's vehicle, and this repository is public.
    /// </remarks>
    public const string Example = "1FTFW1RJ0PFB00000";

    /// <summary>Every VIN issued since 1981 is exactly this long.</summary>
    public const int Length = 17;

    /// <summary>Letters excluded from the standard, because they are misread as digits.</summary>
    public const string ForbiddenLetters = "IOQ";

    /// <summary>Whether text is a plausible VIN. Blank is not a VIN, and is not an error either.</summary>
    public static bool IsWellFormed(string? vin)
    {
        if (string.IsNullOrWhiteSpace(vin))
        {
            return false;
        }

        var trimmed = vin.Trim();
        return trimmed.Length == Length
            && trimmed.All(character => char.IsAsciiLetterOrDigit(character)
                && !ForbiddenLetters.Contains(char.ToUpperInvariant(character), StringComparison.Ordinal));
    }

    /// <summary>Upper case with the surrounding space removed, or null when there is no VIN.</summary>
    public static string? Normalise(string? vin) =>
        string.IsNullOrWhiteSpace(vin) ? null : vin.Trim().ToUpperInvariant();
}
