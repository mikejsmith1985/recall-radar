// Ranks NHTSA's model names by how well they match what somebody typed.
namespace RecallRadar.Domain.Vehicles;

/// <summary>
/// Orders candidate model names by resemblance to a typed one.
/// </summary>
/// <remarks>
/// NHTSA's vocabulary is not the vocabulary on the back of the car: a Mach-E is filed as
/// "MUSTANG MACH-E BEV BEV". Ford alone has 57 names for one model year, so a rejection has to
/// suggest rather than list. Listing the first twenty alphabetically once stopped at "F-59" and hid
/// the very name the person was reaching for, which is the failure this exists to prevent.
/// </remarks>
public static class ModelNameMatcher
{
    /// <summary>Characters that separate words in either vocabulary; NHTSA uses all of them.</summary>
    private static readonly char[] WordSeparators = [' ', '-', '(', ')', ',', '/', '.', '_'];

    /// <summary>
    /// The candidates that best match <paramref name="typed"/>, best first, at most
    /// <paramref name="limit"/> of them. Falls back to an alphabetical sample when nothing
    /// resembles the input, because a readable sample says more than an empty list.
    /// </summary>
    public static IReadOnlyList<string> Rank(string? typed, IReadOnlyCollection<string> candidates, int limit)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        if (limit <= 0 || candidates.Count == 0)
        {
            return [];
        }

        var distinct = candidates
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate))
            .Select(candidate => candidate.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var wanted = Tokenise(typed);
        if (wanted.Count == 0)
        {
            return Sample(distinct, limit);
        }

        var scored = distinct
            .Select(candidate => (Name: candidate, Score: ScoreOf(candidate, wanted)))
            .Where(entry => entry.Score > 0)
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Name, StringComparer.Ordinal)
            .Take(limit)
            .Select(entry => entry.Name)
            .ToList();

        return scored.Count > 0 ? scored : Sample(distinct, limit);
    }

    /// <summary>
    /// How much of the typed name this candidate accounts for, measured in matched characters.
    /// Counting characters rather than words is what stops a one-letter token like "E" scoring as
    /// highly as "MUSTANG", which would put E-450 above the Mach-E.
    /// </summary>
    private static int ScoreOf(string candidate, IReadOnlyCollection<string> wanted)
    {
        var candidateWords = Tokenise(candidate);
        return wanted
            .Where(word => candidateWords.Contains(word, StringComparer.Ordinal))
            .Sum(word => word.Length);
    }

    /// <summary>Splits on every separator either vocabulary uses, upper-cased for exact comparison.</summary>
    private static IReadOnlyCollection<string> Tokenise(string? text) =>
        string.IsNullOrWhiteSpace(text)
            ? []
            : text.ToUpperInvariant()
                .Split(WordSeparators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct(StringComparer.Ordinal)
                .ToList();

    private static IReadOnlyList<string> Sample(IEnumerable<string> candidates, int limit) =>
        [.. candidates.Order(StringComparer.Ordinal).Take(limit)];
}
