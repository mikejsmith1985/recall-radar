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
    public async Task<IReadOnlyList<NhtsaRecall>> GetRecallsAsync(string make, string nhtsaModel, int modelYear, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(BuildRequestUri(make, nhtsaModel, modelYear), cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParseResults(document.RootElement);
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
