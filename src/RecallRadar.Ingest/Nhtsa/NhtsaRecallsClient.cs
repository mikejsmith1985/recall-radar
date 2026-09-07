// Reads recall campaigns for one vehicle from NHTSA's recalls API. GET only (FR-002).
using System.Text.Json;

namespace RecallRadar.Ingest.Nhtsa;

/// <summary>Typed client for <c>recalls/recallsByVehicle</c>. One request per vehicle.</summary>
public sealed class NhtsaRecallsClient(HttpClient httpClient)
{
    private const string RelativePath = "recalls/recallsByVehicle";

    /// <summary>Builds the query the way NHTSA expects it.</summary>
    public static Uri BuildRequestUri(string make, string nhtsaModel, int modelYear) =>
        new($"{RelativePath}?make={Uri.EscapeDataString(make)}&model={Uri.EscapeDataString(nhtsaModel)}&modelYear={modelYear}", UriKind.Relative);

    /// <summary>Fetches and parses every recall campaign for the vehicle.</summary>
    /// <remarks>
    /// A 400 is not always a failure here. NHTSA answers "nothing found" with status 400 and a body
    /// reading <c>{"Count":0,"Message":"Results returned successfully","results":[]}</c>, so a car
    /// too new to have been recalled looks exactly like a malformed request. Treating that as an
    /// error failed the whole load for a 2026 vehicle whose complaints had come back fine. The model
    /// name is checked against NHTSA's own list before a load is queued, so by the time this runs an
    /// empty envelope means the vehicle has no recalls, not that the question was wrong.
    /// </remarks>
    public async Task<IReadOnlyList<NhtsaRecall>> GetRecallsAsync(string make, string nhtsaModel, int modelYear, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(BuildRequestUri(make, nhtsaModel, modelYear), cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode && !IsEmptyResultEnvelope(body))
        {
            response.EnsureSuccessStatusCode();
        }

        using var document = JsonDocument.Parse(body);
        return ParseResults(document.RootElement);
    }

    /// <summary>Whether a body is NHTSA's well-formed way of saying the vehicle has no recalls.</summary>
    /// <remarks>
    /// Deliberately narrow. Only a parseable envelope carrying an empty <c>results</c> array counts;
    /// anything else -- an error page, a truncated response, a body with records in it -- keeps its
    /// status code and fails the load, because those are real problems wearing the same number.
    /// </remarks>
    public static bool IsEmptyResultEnvelope(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && TryGetResultsArray(document.RootElement, out var results)
                && results.GetArrayLength() == 0;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    /// <summary>
    /// Finds the feed's <c>results</c> array, and says so only when the body actually has one.
    /// </summary>
    /// <remarks>
    /// The distinction matters: an error page that happens to be JSON has no <c>results</c> at all,
    /// and treating "no array" the same as "empty array" silently turned a stub's 404 into a
    /// successful load with zero recalls. Absent is not empty.
    /// </remarks>
    private static bool TryGetResultsArray(JsonElement root, out JsonElement results)
    {
        foreach (var property in root.EnumerateObject())
        {
            if (string.Equals(property.Name, NhtsaJson.ResultsProperty, StringComparison.OrdinalIgnoreCase)
                && property.Value.ValueKind == JsonValueKind.Array)
            {
                results = property.Value;
                return true;
            }
        }

        results = default;
        return false;
    }

    /// <summary>Maps the feed's <c>results</c> array onto recall records.</summary>
    public static IReadOnlyList<NhtsaRecall> ParseResults(JsonElement root) =>
        NhtsaJson.ReadResults(root).Select(ParseRecall).ToList();

    private static NhtsaRecall ParseRecall(JsonElement element) => new(
        CampaignNumber: NhtsaJson.ReadText(element, "NHTSACampaignNumber"),
        Component: NhtsaJson.ReadText(element, "Component"),
        Summary: NhtsaJson.ReadText(element, "Summary"),
        Consequence: NhtsaJson.ReadText(element, "Consequence"),
        Remedy: NhtsaJson.ReadText(element, "Remedy"),
        ReportReceivedOn: NhtsaDateParser.ParseSlashDate(NhtsaJson.ReadText(element, "ReportReceivedDate")),
        RawJson: element.GetRawText());
}
