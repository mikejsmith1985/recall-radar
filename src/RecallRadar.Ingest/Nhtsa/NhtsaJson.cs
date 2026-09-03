// Small helpers for reading NHTSA JSON, which mixes numbers and strings for the same field.
using System.Text.Json;

namespace RecallRadar.Ingest.Nhtsa;

/// <summary>
/// NHTSA returns some identifiers as JSON numbers (<c>odiNumber</c>) and some as strings, and the
/// casing of property names differs between feeds. These readers accept either and never throw on
/// a missing field, because a missing field is data to record, not a reason to stop a load.
/// </summary>
public static class NhtsaJson
{
    public const string ResultsProperty = "results";

    /// <summary>Returns the named property as text, or empty when absent or null.</summary>
    public static string ReadText(JsonElement element, string propertyName)
    {
        if (!TryGetPropertyIgnoreCase(element, propertyName, out var property))
        {
            return string.Empty;
        }

        return property.ValueKind switch
        {
            JsonValueKind.String => property.GetString() ?? string.Empty,
            JsonValueKind.Number => property.GetRawText(),
            JsonValueKind.True => bool.TrueString,
            JsonValueKind.False => bool.FalseString,
            _ => string.Empty,
        };
    }

    /// <summary>Enumerates the <c>results</c> array, or nothing when the feed returned no array.</summary>
    public static IEnumerable<JsonElement> ReadResults(JsonElement root)
    {
        if (!TryGetPropertyIgnoreCase(root, ResultsProperty, out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return results.EnumerateArray();
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string propertyName, out JsonElement property)
    {
        property = default;
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }

        foreach (var candidate in element.EnumerateObject())
        {
            if (string.Equals(candidate.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                property = candidate.Value;
                return true;
            }
        }

        return false;
    }
}
