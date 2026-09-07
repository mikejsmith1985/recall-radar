// Names which pool of records a search may draw from, so a rare kind is not buried by a common one.
using System.Text.Json.Serialization;

namespace RecallRadar.Domain.Retrieval;

/// <summary>
/// Which records compete for a place in the results. <see cref="All"/> ranks everything together;
/// <see cref="Campaigns"/> ranks only the official defect records.
/// </summary>
/// <remarks>
/// The two pools exist because they are wildly unequal in size. One vehicle has thousands of owner
/// complaints and a few dozen recalls and investigations, so in a single ranking the complaints win
/// every place and the official record — the thing the owner actually asked about — is never seen.
/// Scoping the second pool lets the rare records be ranked against each other instead.
/// <para>
/// Written as its name, never its number: a results file committed beside the code has to be
/// readable by a person opening it a year later.
/// </para>
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<RetrievalScope>))]
public enum RetrievalScope
{
    All = 1,
    Campaigns = 2,
}

/// <summary>Reads a scope from user input.</summary>
public static class RetrievalScopes
{
    /// <summary>
    /// Parses a scope name case-insensitively. Unknown or blank input yields null rather than a
    /// default, so the caller decides whether that is an error or a fall-back.
    /// </summary>
    public static RetrievalScope? TryParse(string? text)
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

        return Enum.TryParse<RetrievalScope>(trimmed, ignoreCase: true, out var scope) && Enum.IsDefined(scope)
            ? scope
            : null;
    }
}
