// Builds what the model is shown: the rules it must follow, and the records it may quote.
using System.Globalization;
using System.Text;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Api.Answering;

/// <summary>One record as the model sees it, with the id its citations must use.</summary>
/// <param name="DocumentId">The id the model must cite. Stable and numeric, so a citation is checkable.</param>
/// <param name="Kind">Complaint, recall or investigation.</param>
/// <param name="ExternalId">NHTSA's own identifier, shown so the answer can name a real record.</param>
/// <param name="FiledOn">When the record was filed, or null when NHTSA gave no date.</param>
/// <param name="Body">The record's verbatim text. The only text a quote may be drawn from.</param>
public sealed record PromptRecord(string DocumentId, SourceKind Kind, string ExternalId, DateOnly? FiledOn, string Body);

/// <summary>
/// The prompt. The system half states the rules, including that every quote is checked character
/// for character, because telling the model the check exists is what makes it quote rather than
/// paraphrase. The user half carries the question and the records.
/// </summary>
public static class AnswerPrompt
{
    /// <summary>
    /// Kept verbatim and stable so it caches: prompt caching is a prefix match, and an edit here
    /// invalidates every cached request.
    /// </summary>
    public const string SystemPrompt =
        """
        You answer one question about one vehicle, using only the records supplied in the user message.

        Rules:
        - Every claim you make must be supported by a citation.
        - A citation names a record id and quotes a span copied character for character from that record.
        - A record id is the bare number that follows "RECORD". For a record headed "RECORD 1886",
          the documentId is 1886, not "RECORD 1886".
        - Every quote is checked against the record after you answer. A quote that does not appear in
          the record it names is discarded, and with it the claim it supported. A paraphrase, a
          reworded span, a corrected typo, or a span stitched from two places all fail this check.
        - Copy quotes exactly as written, including original capitalisation, spelling and punctuation.
          Many of these records are typed in capitals; reproduce them that way.
        - Keep each quote short: the shortest span that carries the point, not a whole paragraph.
        - Cite only record ids that appear in the supplied records. Never invent one.
        - Say a problem is a known pattern only when several supplied records describe it.
        - If the supplied records do not answer the question, say so plainly and cite nothing.
          An honest "the records here do not show that" is worth more than a confident guess.
        - Write for a vehicle owner, not an engineer. Two or three sentences is usually enough.
        """;

    private const string RecordSeparator = "\n\n";

    /// <summary>Builds the user message: the question, then the records, ordered by id so the text is stable.</summary>
    public static string BuildUserMessage(string vehicleName, string question, IReadOnlyList<PromptRecord> records)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vehicleName);
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        ArgumentNullException.ThrowIfNull(records);

        var builder = new StringBuilder();
        builder.Append("Vehicle: ").AppendLine(vehicleName);
        builder.AppendLine();
        builder.AppendLine("Records:");
        foreach (var record in OrderForStablePrompt(records))
        {
            builder.Append(RecordSeparator).AppendLine(Format(record));
        }

        builder.AppendLine();
        builder.Append("Question: ").Append(question.Trim());
        return builder.ToString();
    }

    /// <summary>
    /// Orders records by id rather than by rank. The prompt prefix is then identical for repeated
    /// questions over the same records, which is what lets the cache hold.
    /// </summary>
    public static IReadOnlyList<PromptRecord> OrderForStablePrompt(IReadOnlyList<PromptRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        return [.. records.OrderBy(record => ParseId(record.DocumentId)).ThenBy(record => record.DocumentId, StringComparer.Ordinal)];
    }

    /// <summary>The bodies the verifier will check against, keyed by the id the model was shown.</summary>
    public static IReadOnlyDictionary<string, string> BuildBodyIndex(IReadOnlyList<PromptRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var index = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var record in records)
        {
            index[record.DocumentId] = record.Body;
        }

        return index;
    }

    private static long ParseId(string documentId) =>
        long.TryParse(documentId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : long.MaxValue;

    private static string Format(PromptRecord record)
    {
        var filedOn = record.FiledOn is { } date ? date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) : "unknown";
        return $"""
            RECORD {record.DocumentId}
            kind: {record.Kind.ToString().ToLowerInvariant()}
            nhtsa id: {record.ExternalId}
            filed: {filedOn}
            text: {record.Body}
            """;
    }
}
