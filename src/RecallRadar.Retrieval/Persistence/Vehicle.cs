// A vehicle the contributor owns, named the way NHTSA names it so ingestion can query by it.
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

    private Vehicle() { }

    /// <summary>
    /// Creates a vehicle, normalising the NHTSA identifiers to upper case because NHTSA
    /// returns them that way and every later comparison relies on an exact match.
    /// </summary>
    public static Vehicle Create(
        string make, string nhtsaModel, int modelYear, string displayName, string? recallModel = null)
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
        };
    }
}
