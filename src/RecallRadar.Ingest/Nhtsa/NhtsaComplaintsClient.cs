// Reads owner complaints for one vehicle from NHTSA's complaints API. GET only (FR-002).
using System.Text.Json;

namespace RecallRadar.Ingest.Nhtsa;

/// <summary>
/// Typed client for <c>complaints/complaintsByVehicle</c>. The API returns every complaint in one
/// response, so a vehicle is one request. Retries and timeouts come from the standard resilience
/// pipeline registered with the client; nothing here retries by hand.
/// </summary>
public sealed class NhtsaComplaintsClient(HttpClient httpClient)
{
    private const string RelativePath = "complaints/complaintsByVehicle";

    /// <summary>Builds the query the way NHTSA expects it: make, model and year as query parameters.</summary>
    public static Uri BuildRequestUri(string make, string nhtsaModel, int modelYear) =>
        new($"{RelativePath}?make={Uri.EscapeDataString(make)}&model={Uri.EscapeDataString(nhtsaModel)}&modelYear={modelYear}", UriKind.Relative);

    /// <summary>Fetches and parses every complaint for the vehicle.</summary>
    public async Task<IReadOnlyList<NhtsaComplaint>> GetComplaintsAsync(string make, string nhtsaModel, int modelYear, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(BuildRequestUri(make, nhtsaModel, modelYear), cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParseResults(document.RootElement);
    }

    /// <summary>Maps the feed's <c>results</c> array onto complaint records, keeping each raw record for audit.</summary>
    public static IReadOnlyList<NhtsaComplaint> ParseResults(JsonElement root) =>
        NhtsaJson.ReadResults(root).Select(ParseComplaint).ToList();

    private static NhtsaComplaint ParseComplaint(JsonElement element) => new(
        OdiNumber: NhtsaJson.ReadText(element, "odiNumber"),
        Components: NhtsaJson.ReadText(element, "components"),
        Summary: NhtsaJson.ReadText(element, "summary"),
        FiledOn: NhtsaDateParser.ParseSlashDate(NhtsaJson.ReadText(element, "dateComplaintFiled")),
        RawJson: element.GetRawText());
}
