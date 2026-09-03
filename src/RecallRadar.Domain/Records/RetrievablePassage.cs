// One searchable passage cut from a source record, with its position so order survives storage.
namespace RecallRadar.Domain.Records;

/// <summary>
/// A chunk of record text. Most records are a single passage; long investigation summaries become
/// several. The caller keeps the link back to the record; this type only knows its own text and order.
/// </summary>
public sealed record RetrievablePassage
{
    public int Ordinal { get; }
    public string Text { get; }

    /// <summary>Creates a passage. Text is trimmed because surrounding whitespace carries no meaning for search.</summary>
    public RetrievablePassage(int ordinal, string text)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ordinal);
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        Ordinal = ordinal;
        Text = text.Trim();
    }
}
