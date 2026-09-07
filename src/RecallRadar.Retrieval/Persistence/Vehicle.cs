// A vehicle the contributor owns, named the way NHTSA names it so ingestion can query by it.
using RecallRadar.Domain.Vehicles;

namespace RecallRadar.Retrieval.Persistence;

/// <summary>
/// One make / model / model-year combination as NHTSA identifies it. The display name is the
/// contributor's own wording ("2014 F-150 SVT Raptor"); the NHTSA model string is what the
/// complaints API actually accepts ("F-150 SUPER CREW").
/// </summary>
public sealed class Vehicle
{
    public const int MinimumModelYear = 1949;

    /// <summary>NHTSA files records a model year ahead of the calendar, and no further.</summary>
    public static int MaximumModelYear => DateTime.UtcNow.Year + 1;

    public int Id { get; private set; }
    public string Make { get; private set; } = string.Empty;
    public string NhtsaModel { get; private set; } = string.Empty;
    public int ModelYear { get; private set; }
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>
    /// The model name the recalls feed accepts, when it differs from the complaints feed's. Stored
    /// so a refresh can repeat the original lookup without reading a configuration file.
    /// </summary>
    public string? RecallModel { get; private set; }

    /// <summary>
    /// The owner's VIN, when they gave one. It is the only thing that says which of the versions
    /// filed under one NHTSA model name this vehicle actually is.
    /// </summary>
    public string? Vin { get; private set; }

    public string? Trim { get; private set; }

    /// <summary>The trim normalised for comparison. Written whenever the trim is.</summary>
    public string? TrimKey { get; private set; }
    public decimal? EngineLitres { get; private set; }
    public int? EngineCylinders { get; private set; }

    /// <summary>This vehicle's own trim and engine, as far as its VIN has been decoded.</summary>
    public VehicleFit Fit => VehicleFit.Create(Trim, EngineLitres, EngineCylinders);

    private Vehicle() { }

    /// <summary>
    /// Creates a vehicle, normalising the NHTSA identifiers to upper case because NHTSA
    /// returns them that way and every later comparison relies on an exact match.
    /// </summary>
    public static Vehicle Create(
        string make, string nhtsaModel, int modelYear, string displayName, string? recallModel = null, string? vin = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(make);
        ArgumentException.ThrowIfNullOrWhiteSpace(nhtsaModel);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);
        if (modelYear < MinimumModelYear)
        {
            throw new ArgumentOutOfRangeException(nameof(modelYear), $"Model year must be {MinimumModelYear} or later.");
        }

        return new Vehicle
        {
            Make = make.Trim().ToUpperInvariant(),
            NhtsaModel = nhtsaModel.Trim().ToUpperInvariant(),
            ModelYear = modelYear,
            DisplayName = displayName.Trim(),
            RecallModel = string.IsNullOrWhiteSpace(recallModel) ? null : recallModel.Trim().ToUpperInvariant(),
            Vin = NormaliseVin(vin),
        };
    }

    /// <summary>Records what the owner's VIN decoded to, so records can be matched against it.</summary>
    public void DescribeFit(VehicleFit fit)
    {
        ArgumentNullException.ThrowIfNull(fit);
        Trim = fit.Trim;
        TrimKey = fit.TrimKey;
        EngineLitres = fit.EngineLitres;
        EngineCylinders = fit.EngineCylinders;
    }

    /// <summary>Sets the VIN on a vehicle registered before one was given.</summary>
    public void RecordVin(string? vin) => Vin = NormaliseVin(vin);

    /// <summary>A VIN is upper case with no spaces, because every comparison relies on an exact match.</summary>
    private static string? NormaliseVin(string? vin) =>
        string.IsNullOrWhiteSpace(vin) ? null : vin.Trim().ToUpperInvariant();
}
