// Reads NHTSA's investigations flat file and keeps only the rows about the configured vehicles.
using System.IO.Compression;
using System.Text;
using RecallRadar.Ingest.Config;

namespace RecallRadar.Ingest.Nhtsa;

/// <summary>
/// FLAT_INV.zip holds one Latin-1 text file with eleven tab-separated columns per row:
/// action number, make, model, year, component, manufacturer, open date, close date,
/// campaign number, subject, summary. The campaign number is what links an investigation to a
/// recall, which is why this file exists in the pipeline at all.
/// </summary>
public static class InvestigationFlatFileParser
{
    public const int ColumnCount = 11;
    private const char ColumnSeparator = '\t';
    private static readonly Encoding FlatFileEncoding = Encoding.Latin1;

    /// <summary>Opens the zip, reads the first entry, and returns the rows matching any configured vehicle.</summary>
    public static IReadOnlyList<NhtsaInvestigationRow> ParseZip(byte[] zipBytes, IReadOnlyCollection<VehicleRegistration> vehicles)
    {
        ArgumentNullException.ThrowIfNull(zipBytes);
        using var zipStream = new MemoryStream(zipBytes, writable: false);
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        var entry = archive.Entries.FirstOrDefault()
            ?? throw new InvalidDataException("The investigations archive contains no entries.");
        using var reader = new StreamReader(entry.Open(), FlatFileEncoding);
        return ParseLines(ReadLines(reader), vehicles);
    }

    /// <summary>Parses rows from already-decoded lines; rows about other vehicles are dropped.</summary>
    public static IReadOnlyList<NhtsaInvestigationRow> ParseLines(IEnumerable<string> lines, IReadOnlyCollection<VehicleRegistration> vehicles)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(vehicles);
        var matches = new List<NhtsaInvestigationRow>();
        foreach (var line in lines)
        {
            var row = ParseLine(line);
            if (row is null)
            {
                continue;
            }

            if (vehicles.Any(vehicle => row.MatchesVehicle(vehicle.Make, vehicle.NhtsaModel, vehicle.ModelYear)))
            {
                matches.Add(row);
            }
        }

        return matches;
    }

    /// <summary>Parses one line, or returns null for a line that does not have the expected columns.</summary>
    public static NhtsaInvestigationRow? ParseLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return null;
        }

        var columns = line.Split(ColumnSeparator);
        if (columns.Length < ColumnCount || !int.TryParse(columns[3], out var modelYear))
        {
            return null;
        }

        return new NhtsaInvestigationRow(
            ActionNumber: columns[0].Trim(),
            Make: columns[1].Trim(),
            Model: columns[2].Trim(),
            ModelYear: modelYear,
            Component: columns[4].Trim(),
            Manufacturer: columns[5].Trim(),
            OpenedOn: NhtsaDateParser.ParseCompactDate(columns[6]),
            ClosedOn: NhtsaDateParser.ParseCompactDate(columns[7]),
            CampaignNumber: columns[8].Trim(),
            Subject: columns[9].Trim(),
            Summary: string.Join(ColumnSeparator, columns[10..]).Trim(),
            RawLine: line);
    }

    private static IEnumerable<string> ReadLines(TextReader reader)
    {
        while (reader.ReadLine() is { } line)
        {
            yield return line;
        }
    }
}
