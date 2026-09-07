// What separates one version of a model from another: its trim and its engine.
using System.Globalization;

namespace RecallRadar.Domain.Vehicles;

/// <summary>
/// The trim and engine a record belongs to, decoded from a VIN.
/// </summary>
/// <remarks>
/// NHTSA files every complaint under a model name that stops at the body style: a 2023 F-150 is
/// "F-150 (SUPER CREW) GAS" whether it has a 2.7 litre V6 or the Raptor R's supercharged 5.2 V8.
/// An engine problem on one of those has nothing to do with the other, so answering about a Raptor R
/// from the whole F-150 pool is answering about a different truck.
/// <para>
/// Every field is optional because NHTSA's own decoder leaves them blank often enough that requiring
/// them would throw away most of the corpus. What is known is compared; what is not is not held
/// against a record.
/// </para>
/// </remarks>
public sealed record VehicleFit(string? Trim, decimal? EngineLitres, int? EngineCylinders)
{
    /// <summary>Nothing is known about this record's trim or engine.</summary>
    public static readonly VehicleFit Unknown = new(null, null, null);

    /// <summary>
    /// The trim reduced to letters and digits in lower case, which is what a stored comparison uses.
    /// </summary>
    /// <remarks>
    /// NHTSA writes one trim as "SuperCrew-Raptor" in a model year and "Supercrew Raptor" in the
    /// next. Normalising in the database rather than in the query keeps the comparison a plain
    /// equality an index can serve.
    /// </remarks>
    public string? TrimKey => Normalise(Trim);

    /// <summary>Whether anything at all is known, and so whether this fit can narrow anything.</summary>
    public bool IsKnown => Trim is not null || EngineLitres is not null || EngineCylinders is not null;

    /// <summary>Builds a fit, treating blank text and non-positive numbers as "not known".</summary>
    public static VehicleFit Create(string? trim, decimal? engineLitres, int? engineCylinders) => new(
        string.IsNullOrWhiteSpace(trim) ? null : trim.Trim(),
        engineLitres is > 0 ? engineLitres : null,
        engineCylinders is > 0 ? engineCylinders : null);

    /// <summary>
    /// Whether a record could belong to the same version of the vehicle as <paramref name="owned"/>.
    /// </summary>
    /// <remarks>
    /// Fields only disagree when both sides state them. A record whose engine NHTSA could not decode
    /// is kept rather than discarded, because the alternative is silently throwing away a quarter of
    /// the corpus over a blank field. Trim is compared case-insensitively after normalisation,
    /// because NHTSA writes the same trim as "SuperCrew-Raptor" and "Supercrew Raptor" in different
    /// model years.
    /// </remarks>
    public bool CouldBe(VehicleFit owned)
    {
        ArgumentNullException.ThrowIfNull(owned);
        return Agrees(Normalise(Trim), Normalise(owned.Trim))
            && Agrees(EngineLitres, owned.EngineLitres)
            && Agrees(EngineCylinders, owned.EngineCylinders);
    }

    /// <summary>How to say this fit in a sentence, for a badge on a record or a caption.</summary>
    public string Describe()
    {
        var parts = new List<string>();
        if (Trim is not null)
        {
            parts.Add(Trim);
        }

        if (EngineLitres is not null)
        {
            parts.Add($"{EngineLitres.Value.ToString("0.#", CultureInfo.InvariantCulture)}L");
        }

        if (EngineCylinders is not null)
        {
            parts.Add($"{EngineCylinders.Value} cyl");
        }

        return parts.Count == 0 ? "trim not decoded" : string.Join(" · ", parts);
    }

    private static bool Agrees<TValue>(TValue? mine, TValue? theirs) where TValue : struct, IEquatable<TValue> =>
        mine is null || theirs is null || mine.Value.Equals(theirs.Value);

    private static bool Agrees(string? mine, string? theirs) =>
        mine is null || theirs is null || string.Equals(mine, theirs, StringComparison.Ordinal);

    /// <summary>
    /// Reduces a trim to letters and digits in lower case, so punctuation and spacing cannot make
    /// two spellings of one trim look like two trims.
    /// </summary>
    private static string? Normalise(string? trim)
    {
        if (string.IsNullOrWhiteSpace(trim))
        {
            return null;
        }

        var kept = trim.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray();
        return kept.Length == 0 ? null : new string(kept);
    }
}
