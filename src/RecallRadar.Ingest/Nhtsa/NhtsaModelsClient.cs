// Reads the model names NHTSA files complaints under, so a registered vehicle can be checked. GET only.
using System.Text.Json;

namespace RecallRadar.Ingest.Nhtsa;

/// <summary>
/// Typed client for <c>products/vehicle/models</c>. NHTSA files the F-150 under body styles
/// ("F-150 SUPER CREW") and returns nothing for the bare family name, so a registration is
/// validated against this list before any load is attempted.
/// </summary>
public sealed class NhtsaModelsClient(HttpClient httpClient)
{
    private const string RelativePath = "products/vehicle/models";
    private const string ComplaintsIssueType = "c";

    /// <summary>Builds the query for the complaint-issue model list of a make and year.</summary>
    public static Uri BuildRequestUri(string make, int modelYear) =>
        new($"{RelativePath}?modelYear={modelYear}&make={Uri.EscapeDataString(make)}&issueType={ComplaintsIssueType}", UriKind.Relative);

    /// <summary>Fetches the distinct model strings NHTSA accepts for the make and year.</summary>
    public async Task<IReadOnlySet<string>> GetModelNamesAsync(string make, int modelYear, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(BuildRequestUri(make, modelYear), cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParseModelNames(document.RootElement);
    }

    /// <summary>Collects the <c>model</c> field of every result, case-insensitively de-duplicated.</summary>
    public static IReadOnlySet<string> ParseModelNames(JsonElement root)
    {
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in NhtsaJson.ReadResults(root))
        {
            var model = NhtsaJson.ReadText(element, "model").Trim();
            if (model.Length > 0)
            {
                names.Add(model);
            }
        }

        return names;
    }
}
