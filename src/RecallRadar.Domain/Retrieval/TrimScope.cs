// Whether a search is confined to the owner's own trim and engine, or open to every version.
using System.Text.Json.Serialization;

namespace RecallRadar.Domain.Retrieval;

/// <summary>
/// How closely a search has to match the vehicle's own trim and engine.
/// </summary>
/// <remarks>
/// <see cref="ThisTrim"/> is the honest default: a supercharged 5.2 litre V8 shares almost nothing
/// mechanically with the 2.7 litre V6 filed under the same NHTSA model name. But a rare trim can
/// have no complaints at all, and an empty page is not a useful answer, so <see cref="AllTrims"/>
/// opens the search to every version and each record says which one it came from.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<TrimScope>))]
public enum TrimScope
{
    ThisTrim = 1,
    AllTrims = 2,
}

/// <summary>Reads a trim scope from user input.</summary>
public static class TrimScopes
{
    /// <summary>
    /// Parses a scope name case-insensitively. Unknown or blank input yields null rather than a
    /// default, so the caller decides whether that is an error or a fall-back.
    /// </summary>
    public static TrimScope? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var trimmed = text.Trim();
        // Names only. Enum.TryParse also accepts the underlying number, which would make "1" a
        // silent alias for the first member -- an input the contract never documented and nobody
        // could read back.
        if (!char.IsAsciiLetter(trimmed[0]))
        {
            return null;
        }

        return Enum.TryParse<TrimScope>(trimmed, ignoreCase: true, out var scope) && Enum.IsDefined(scope)
            ? scope
            : null;
    }
}
