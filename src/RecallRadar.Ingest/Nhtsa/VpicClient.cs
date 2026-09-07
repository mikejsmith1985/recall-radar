// Reads NHTSA's own VIN decoder for the trim and engine a complaint's VIN belongs to. GET only.
using System.Text.Json;
using RecallRadar.Domain.Vehicles;

namespace RecallRadar.Ingest.Nhtsa;

/// <summary>
/// Typed client for vPIC's <c>DecodeVinValues</c>.
/// </summary>
/// <remarks>
/// vPIC is NHTSA's own product catalogue, and the only thing that turns a VIN into a trim and an
/// engine. It is a separate service from the complaints API and so has its own base address.
/// <para>
/// One VIN per request, deliberately. vPIC has a batch endpoint, but it is a POST, and no mutating
/// verb may appear in this folder (FR-002) — a rule worth more than the round trips it costs, since
/// a descriptor is only eight characters and a vehicle has about fifty distinct ones.
/// </para>
/// </remarks>
public sealed class VpicClient(HttpClient httpClient)
{
    private const string RelativePath = "vehicles/DecodeVinValues";

    /// <summary>
    /// Builds the decode query. The model year is passed explicitly because a descriptor stops
    /// short of the position that encodes it, and vPIC decodes differently between years.
    /// </summary>
    public static Uri BuildRequestUri(string vinOrDescriptor, int? modelYear)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(vinOrDescriptor);
        var query = $"{RelativePath}/{Uri.EscapeDataString(vinOrDescriptor.Trim())}?format=json";
        return new Uri(modelYear is null ? query : $"{query}&modelyear={modelYear}", UriKind.Relative);
    }

    /// <summary>Decodes one VIN or descriptor into the trim and engine it belongs to.</summary>
    public async Task<VehicleFit> GetFitAsync(string vinOrDescriptor, int? modelYear, CancellationToken cancellationToken)
    {
        using var response = await httpClient.GetAsync(BuildRequestUri(vinOrDescriptor, modelYear), cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        return ParseFit(document.RootElement);
    }

    /// <summary>
    /// Reads the trim and engine out of a decode response.
    /// </summary>
    /// <remarks>
    /// A partial VIN always comes back with an error code, because it is incomplete by definition,
    /// and the fields are populated anyway. So the error code is ignored and the fields are read for
    /// what they hold: a blank one means vPIC does not know, which <see cref="VehicleFit"/> treats
    /// as "do not exclude anything on this".
    /// </remarks>
    public static VehicleFit ParseFit(JsonElement root)
    {
        foreach (var result in NhtsaJson.ReadResults(root))
        {
            return VehicleFit.Create(
                NhtsaJson.ReadText(result, "Trim"),
                ReadDecimal(result, "DisplacementL"),
                ReadInteger(result, "EngineCylinders"));
        }

        return VehicleFit.Unknown;
    }

    private static decimal? ReadDecimal(JsonElement element, string propertyName) =>
        decimal.TryParse(
            NhtsaJson.ReadText(element, propertyName),
            System.Globalization.NumberStyles.Number,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value) ? value : null;

    private static int? ReadInteger(JsonElement element, string propertyName) =>
        int.TryParse(
            NhtsaJson.ReadText(element, propertyName),
            System.Globalization.NumberStyles.Integer,
            System.Globalization.CultureInfo.InvariantCulture,
            out var value) ? value : null;
}
