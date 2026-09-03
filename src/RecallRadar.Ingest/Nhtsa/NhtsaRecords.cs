// The NHTSA record shapes as received, before they become stored source documents.
namespace RecallRadar.Ingest.Nhtsa;

/// <summary>One owner complaint from the complaints API. The summary is the owner's own words.</summary>
public sealed record NhtsaComplaint(
    string OdiNumber,
    string Components,
    string Summary,
    DateOnly? FiledOn,
    string RawJson)
{
    public const int TitleCharacters = 80;

    /// <summary>The first line of the record listing: the opening of the summary.</summary>
    public string BuildTitle()
    {
        var singleLine = Summary.Trim().ReplaceLineEndings(" ");
        return singleLine.Length <= TitleCharacters ? singleLine : singleLine[..TitleCharacters];
    }

    public bool HasSummary => !string.IsNullOrWhiteSpace(Summary);
}

/// <summary>One recall campaign from the recalls API.</summary>
public sealed record NhtsaRecall(
    string CampaignNumber,
    string Component,
    string Summary,
    string Consequence,
    string Remedy,
    DateOnly? ReportReceivedOn,
    string RawJson)
{
    private const string SectionSeparator = "\n\n";

    /// <summary>The searchable and quotable text: the three narrative sections, each kept verbatim.</summary>
    public string BuildBody() =>
        string.Join(SectionSeparator, new[] { Summary, Consequence, Remedy }.Where(section => !string.IsNullOrWhiteSpace(section)));

    public string BuildTitle() => $"{CampaignNumber} {Component}".Trim();
}

/// <summary>One row of the investigations flat file: one investigation × one make/model/year/component.</summary>
public sealed record NhtsaInvestigationRow(
    string ActionNumber,
    string Make,
    string Model,
    int ModelYear,
    string Component,
    string Manufacturer,
    DateOnly? OpenedOn,
    DateOnly? ClosedOn,
    string CampaignNumber,
    string Subject,
    string Summary,
    string RawLine)
{
    public bool HasCampaign => !string.IsNullOrWhiteSpace(CampaignNumber);

    /// <summary>
    /// Whether this row is about the given vehicle. The flat file names the truck family
    /// ("F-150") while the complaints API needs the body style ("F-150 SUPER CREW"), so a configured
    /// model matches when it equals the row's model or starts with it followed by a space.
    /// </summary>
    public bool MatchesVehicle(string make, string nhtsaModel, int modelYear)
    {
        if (ModelYear != modelYear || !string.Equals(Make, make, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var isExactModel = string.Equals(Model, nhtsaModel, StringComparison.OrdinalIgnoreCase);
        var isFamilyModel = nhtsaModel.StartsWith(Model + " ", StringComparison.OrdinalIgnoreCase);
        return isExactModel || isFamilyModel;
    }
}
