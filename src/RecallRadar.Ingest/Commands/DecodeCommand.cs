// Works out which version of a model each complaint belongs to, from the VIN NHTSA files with it.
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RecallRadar.Domain.Vehicles;
using RecallRadar.Ingest.Nhtsa;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Ingest.Commands;

/// <summary>
/// Fills in the trim and engine of every complaint that has a VIN and no decode yet.
/// </summary>
/// <remarks>
/// NHTSA files complaints under a model name that stops at the body style, so a 2023 F-150 covers
/// both a 2.7 litre V6 and the Raptor R's supercharged 5.2 V8. The VIN each complaint carries is the
/// only thing that separates them, and vPIC — NHTSA's own catalogue — is the only thing that reads
/// it. This pass reads it once per distinct descriptor rather than once per record: a vehicle with
/// two thousand complaints has about fifty.
/// <para>
/// Answers are remembered in <c>vin_decodes</c>, so a second run costs nothing and a re-load does
/// not lean again on a service that belongs to somebody else.
/// </para>
/// </remarks>
public sealed class DecodeCommand(
    RecallRadarDbContext database,
    VpicClient vpic,
    TimeProvider clock,
    ILogger<DecodeCommand> logger)
{
    /// <summary>How many documents to update between saves, so a failed run keeps what it earned.</summary>
    public const int SaveInterval = 200;

    /// <summary>Decodes every complaint awaiting one, optionally limited to a single vehicle.</summary>
    public async Task<DecodeReport> DecodeAsync(string? vehicleDisplayName, CancellationToken cancellationToken)
    {
        var vehicles = await LoadVehiclesAsync(vehicleDisplayName, cancellationToken);
        var report = DecodeReport.Empty;
        foreach (var vehicle in vehicles)
        {
            report = report.Add(await DecodeVehicleAsync(vehicle, cancellationToken));
        }

        return report;
    }

    private async Task<DecodeReport> DecodeVehicleAsync(Vehicle vehicle, CancellationToken cancellationToken)
    {
        var wasOwnVinDecoded = await DecodeOwnVinAsync(vehicle, cancellationToken);
        var documents = await database.SourceDocuments
            .Where(document => document.VehicleId == vehicle.Id && document.Kind == SourceKind.Complaint)
            .ToListAsync(cancellationToken);

        var described = 0;
        var withoutVin = 0;
        var pending = 0;
        foreach (var document in documents)
        {
            var descriptor = VinDescriptor.From(ReadVin(document.RawPayload));
            if (descriptor is null)
            {
                withoutVin++;
                continue;
            }

            if (document.VinDescriptor == descriptor)
            {
                continue;
            }

            document.DescribeFit(descriptor, await ResolveAsync(descriptor, vehicle.ModelYear, cancellationToken));
            described++;
            if (++pending >= SaveInterval)
            {
                await database.SaveChangesAsync(cancellationToken);
                pending = 0;
            }
        }

        await database.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Decoded {Described} of {Total} complaints for {Vehicle}.", described, documents.Count, vehicle.DisplayName);
        return new DecodeReport(1, wasOwnVinDecoded ? 1 : 0, described, withoutVin);
    }

    /// <summary>Decodes the owner's own VIN, which is what every record is then compared against.</summary>
    private async Task<bool> DecodeOwnVinAsync(Vehicle vehicle, CancellationToken cancellationToken)
    {
        if (vehicle.Vin is null || vehicle.Fit.IsKnown)
        {
            return false;
        }

        // The whole VIN, not its descriptor: the owner gave all seventeen characters, and the model
        // year is passed anyway so vPIC answers the same way it does for a partial one.
        vehicle.DescribeFit(await vpic.GetFitAsync(vehicle.Vin, vehicle.ModelYear, cancellationToken));
        await database.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>The remembered decode for a descriptor, asking vPIC only when nothing is remembered.</summary>
    private async Task<VehicleFit> ResolveAsync(string descriptor, int modelYear, CancellationToken cancellationToken)
    {
        var remembered = await database.VinDecodes
            .FirstOrDefaultAsync(entry => entry.Descriptor == descriptor && entry.ModelYear == modelYear, cancellationToken);
        if (remembered is not null)
        {
            return remembered.Fit;
        }

        var fit = await vpic.GetFitAsync(descriptor, modelYear, cancellationToken);
        database.VinDecodes.Add(VinDecode.Create(descriptor, modelYear, fit, clock.GetUtcNow()));
        await database.SaveChangesAsync(cancellationToken);
        return fit;
    }

    private Task<List<Vehicle>> LoadVehiclesAsync(string? displayName, CancellationToken cancellationToken)
    {
        var query = database.Vehicles.AsQueryable();
        if (!string.IsNullOrWhiteSpace(displayName))
        {
            var trimmed = displayName.Trim();
            query = query.Where(vehicle => vehicle.DisplayName == trimmed);
        }

        return query.OrderBy(vehicle => vehicle.Id).ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Reads the VIN out of a stored complaint payload. A payload that is not a complaint, or has no
    /// VIN, yields null rather than throwing: a record with no VIN is a normal record.
    /// </summary>
    public static string? ReadVin(string? rawPayload)
    {
        if (string.IsNullOrWhiteSpace(rawPayload))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(rawPayload);
            var vin = NhtsaJson.ReadText(document.RootElement, "vin").Trim();
            return vin.Length == 0 ? null : vin;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
