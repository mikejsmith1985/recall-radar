// Names the three ways a query can be matched against stored records.
namespace RecallRadar.Domain.Retrieval;

/// <summary>
/// Dense matches by meaning (embedding distance), Sparse by keyword (full-text rank), Hybrid fuses
/// both. The evaluation reports each separately so the value of fusion is measured, not assumed.
/// </summary>
public enum RetrievalMode
{
    Dense = 1,
    Sparse = 2,
    Hybrid = 3,
}

/// <summary>Helpers for reading a mode from user input and knowing what it needs.</summary>
public static class RetrievalModes
{
    /// <summary>Parses a mode name case-insensitively; unknown or blank input yields null rather than a default, so the caller decides.</summary>
    public static RetrievalMode? TryParse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return Enum.TryParse<RetrievalMode>(text.Trim(), ignoreCase: true, out var mode) && Enum.IsDefined(mode)
            ? mode
            : null;
    }

    /// <summary>Dense and Hybrid need embeddings; Sparse works on text alone, which is the mode available before the embedding key exists.</summary>
    public static bool RequiresEmbeddings(this RetrievalMode mode) => mode is RetrievalMode.Dense or RetrievalMode.Hybrid;
}
