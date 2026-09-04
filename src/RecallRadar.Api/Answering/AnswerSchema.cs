// The JSON shape the model must return, so an answer is parsed rather than interpreted.
using System.Text.Json;

namespace RecallRadar.Api.Answering;

/// <summary>
/// The structured-output schema from research R5. Constraining the shape is what makes the answer
/// checkable: citations arrive as data with a record id and a quote, not as prose a parser has to
/// guess at.
/// </summary>
public static class AnswerSchema
{
    public const string AnswerProperty = "answer";
    public const string IsKnownPatternProperty = "isKnownPattern";
    public const string CitationsProperty = "citations";
    public const string DocumentIdProperty = "documentId";
    public const string QuoteProperty = "quote";
    public const string LinkedCampaignsProperty = "linkedCampaigns";

    /// <summary>Builds the schema. Every field is required, so a partial answer is a parse failure rather than a silent gap.</summary>
    public static IReadOnlyDictionary<string, JsonElement> Build() => new Dictionary<string, JsonElement>
    {
        ["type"] = Element("object"),
        ["additionalProperties"] = Element(false),
        ["required"] = Element(new[] { AnswerProperty, IsKnownPatternProperty, CitationsProperty, LinkedCampaignsProperty }),
        ["properties"] = Element(new Dictionary<string, object>
        {
            [AnswerProperty] = new Dictionary<string, object>
            {
                ["type"] = "string",
                ["description"] = "The answer to the owner's question, in plain language, drawn only from the records supplied.",
            },
            [IsKnownPatternProperty] = new Dictionary<string, object>
            {
                ["type"] = "boolean",
                ["description"] = "True when the supplied records show other owners reporting the same problem.",
            },
            [CitationsProperty] = new Dictionary<string, object>
            {
                ["type"] = "array",
                ["description"] = "Every claim's supporting evidence. Each quote is checked character for character.",
                ["items"] = new Dictionary<string, object>
                {
                    ["type"] = "object",
                    ["additionalProperties"] = false,
                    ["required"] = new[] { DocumentIdProperty, QuoteProperty },
                    ["properties"] = new Dictionary<string, object>
                    {
                        [DocumentIdProperty] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "The RECORD id exactly as it appears in the supplied records.",
                        },
                        [QuoteProperty] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["description"] = "A span copied verbatim from that record. Not a paraphrase, not a summary.",
                        },
                    },
                },
            },
            [LinkedCampaignsProperty] = new Dictionary<string, object>
            {
                ["type"] = "array",
                ["description"] = "NHTSA recall campaign numbers named in the supplied records, if any.",
                ["items"] = new Dictionary<string, object> { ["type"] = "string" },
            },
        }),
    };

    private static JsonElement Element(object value) => JsonSerializer.SerializeToElement(value);
}
