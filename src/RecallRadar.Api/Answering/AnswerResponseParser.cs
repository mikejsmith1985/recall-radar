// Reads the model's JSON into domain types, treating anything malformed as a failure to report.
using System.Text.Json;
using RecallRadar.Domain.Grounding;

namespace RecallRadar.Api.Answering;

/// <summary>What the model said, before any of it has been checked against the records.</summary>
/// <param name="AnswerText">The prose answer.</param>
/// <param name="IsKnownPattern">The model's claim that several records describe the same problem.</param>
/// <param name="Citations">The evidence it offered, still unverified.</param>
/// <param name="LinkedCampaigns">Recall campaign numbers it named.</param>
public sealed record ParsedAnswer(
    string AnswerText,
    bool IsKnownPattern,
    IReadOnlyList<Citation> Citations,
    IReadOnlyList<string> LinkedCampaigns);

/// <summary>Either a parsed answer or the reason there is none.</summary>
public sealed record AnswerParseResult(ParsedAnswer? Answer, string? Failure)
{
    public bool IsParsed => Answer is not null;

    public static AnswerParseResult Parsed(ParsedAnswer answer) => new(answer, null);

    public static AnswerParseResult Failed(string reason) => new(null, reason);
}

/// <summary>
/// Turns the model's structured output into domain types. A missing or malformed field yields a
/// failure result rather than an exception: the endpoint has to answer the reader either way, and
/// a model that returned something unusable is a reportable state, not a server fault.
/// </summary>
public static class AnswerResponseParser
{
    public const string MalformedJsonReason = "The model's reply was not the JSON the schema asked for.";
    public const string MissingAnswerReason = "The model's reply carried no answer text.";

    /// <summary>The label the prompt heads each record with, which a model may echo into its citation.</summary>
    private const string RecordLabel = "RECORD ";

    /// <summary>Parses the JSON text of a structured-output response.</summary>
    public static AnswerParseResult Parse(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return AnswerParseResult.Failed(MalformedJsonReason);
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            return ReadRoot(document.RootElement);
        }
        catch (JsonException)
        {
            return AnswerParseResult.Failed(MalformedJsonReason);
        }
    }

    private static AnswerParseResult ReadRoot(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return AnswerParseResult.Failed(MalformedJsonReason);
        }

        if (!root.TryGetProperty(AnswerSchema.AnswerProperty, out var answerText)
            || answerText.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(answerText.GetString()))
        {
            return AnswerParseResult.Failed(MissingAnswerReason);
        }

        return AnswerParseResult.Parsed(new ParsedAnswer(
            answerText.GetString()!,
            root.TryGetProperty(AnswerSchema.IsKnownPatternProperty, out var pattern) && pattern.ValueKind == JsonValueKind.True,
            ReadCitations(root),
            ReadStrings(root, AnswerSchema.LinkedCampaignsProperty)));
    }

    /// <summary>Reads the citation array. A malformed entry is skipped here and never reaches the verifier.</summary>
    private static IReadOnlyList<Citation> ReadCitations(JsonElement root)
    {
        if (!root.TryGetProperty(AnswerSchema.CitationsProperty, out var citations) || citations.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var parsed = new List<Citation>();
        foreach (var entry in citations.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var documentId = NormaliseDocumentId(ReadString(entry, AnswerSchema.DocumentIdProperty));
            var quote = ReadString(entry, AnswerSchema.QuoteProperty);
            parsed.Add(new Citation(documentId, quote));
        }

        return parsed;
    }

    private static IReadOnlyList<string> ReadStrings(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return
        [
            .. array.EnumerateArray()
                .Where(entry => entry.ValueKind == JsonValueKind.String)
                .Select(entry => entry.GetString()!)
                .Where(value => !string.IsNullOrWhiteSpace(value)),
        ];
    }

    /// <summary>
    /// Strips the "RECORD" label the prompt heads each record with.
    /// </summary>
    /// <remarks>
    /// A model that answers "RECORD 1886" has named the record it was shown, using the words the
    /// prompt used. That is not a fabricated citation, and rejecting it discarded real evidence in
    /// a live run. The quote itself is still checked character for character; only the label the
    /// prompt introduced is forgiven here.
    /// </remarks>
    public static string NormaliseDocumentId(string documentId)
    {
        ArgumentNullException.ThrowIfNull(documentId);
        var trimmed = documentId.Trim();
        return trimmed.StartsWith(RecordLabel, StringComparison.OrdinalIgnoreCase)
            ? trimmed[RecordLabel.Length..].Trim()
            : trimmed;
    }

    private static string ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;
}
