// What a decode pass did, in the shape the command line and the load report print.
namespace RecallRadar.Ingest.Commands;

/// <summary>Counts from one decode pass.</summary>
/// <param name="Vehicles">How many vehicles were looked at.</param>
/// <param name="OwnVinsDecoded">How many owners' own VINs were decoded, which records are matched against.</param>
/// <param name="ComplaintsDescribed">How many complaints gained a trim and engine.</param>
/// <param name="ComplaintsWithoutVin">How many carried no VIN, and so can never be narrowed.</param>
public sealed record DecodeReport(int Vehicles, int OwnVinsDecoded, int ComplaintsDescribed, int ComplaintsWithoutVin)
{
    public static readonly DecodeReport Empty = new(0, 0, 0, 0);

    /// <summary>Adds another vehicle's counts to this one.</summary>
    public DecodeReport Add(DecodeReport other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new DecodeReport(
            Vehicles + other.Vehicles,
            OwnVinsDecoded + other.OwnVinsDecoded,
            ComplaintsDescribed + other.ComplaintsDescribed,
            ComplaintsWithoutVin + other.ComplaintsWithoutVin);
    }

    /// <summary>The contract output for the command line, one fact per line.</summary>
    public IEnumerable<string> FormatLines()
    {
        yield return $"vehicles: {Vehicles}, own VINs decoded: {OwnVinsDecoded}";
        yield return $"complaints: described {ComplaintsDescribed}, no VIN on file {ComplaintsWithoutVin}";
    }
}
