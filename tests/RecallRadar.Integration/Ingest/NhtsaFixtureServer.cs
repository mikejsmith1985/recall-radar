// A recorded NHTSA stand-in: WireMock serving trimmed real API responses and a synthetic flat file.
using System.IO.Compression;
using System.Text;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace RecallRadar.Integration.Ingest;

/// <summary>
/// The integration suite never talks to NHTSA (Article V). The JSON fixtures were captured from
/// the live feeds on 2026-09-03 and trimmed; the flat file is built here so the test controls
/// exactly which investigations and campaign links exist.
/// </summary>
public sealed class NhtsaFixtureServer : IDisposable
{
    public const string FlatFilePath = "/odi/ffdd/inv/FLAT_INV.zip";

    /// <summary>
    /// The fixture vehicle is a 2012 Explorer, not 2013: the container database is shared by the
    /// whole integration collection, and the schema tests already insert FORD / EXPLORER / 2013.
    /// </summary>
    public const int FixtureModelYear = 2012;
    private const string ComplaintsPath = "/complaints/complaintsByVehicle";
    private const string RecallsPath = "/recalls/recallsByVehicle";
    private const string ModelsPath = "/products/vehicle/models";
    private const string JsonContentType = "application/json";

    private static readonly string FixtureDirectory = Path.Combine(AppContext.BaseDirectory, "Fixtures", "nhtsa");

    /// <summary>Rows for the flat file: the 2013 Explorer's exhaust investigation (no campaign), a linked one, a truck row and another make.</summary>
    public static readonly string[] FlatFileRows =
    [
        "EA17002\tFORD\tEXPLORER\t2012\tENGINE AND ENGINE COOLING:EXHAUST SYSTEM\tFord Motor Company\t20170727\t20230117\t\tExhaust Odor in Passenger Cab\tDuring the EA17-002 investigation, the agency reviewed reports of exhaust odors in the passenger cabin.",
        "EA17002\tFORD\tEXPLORER\t2012\tSTRUCTURE:BODY\tFord Motor Company\t20170727\t20230117\t\tExhaust Odor in Passenger Cab\tDuring the EA17-002 investigation, the agency reviewed reports of exhaust odors in the passenger cabin.",
        "PE16008\tFORD\tEXPLORER\t2012\tSUSPENSION:REAR\tFord Motor Company\t20160601\t20190131\t19V435000\tRear Toe Link Fracture\tThe agency opened a preliminary evaluation of rear toe link fractures.",
        "PE16003\tFORD\tF-150\t2014\tPOWER TRAIN\tFord Motor Company\t20160301\t\t16V123000\tDownshift\tUnexpected downshifts were reported.",
        "PE99999\tTOYOTA\tTUNDRA\t2014\tPOWER TRAIN\tToyota\t20160301\t\t\tSubject\tSummary.",
    ];

    private readonly WireMockServer _server = WireMockServer.Start();

    public string BaseUrl => _server.Url! + "/";
    public string FlatFileUrl => _server.Url! + FlatFilePath;

    public NhtsaFixtureServer()
    {
        MapJson(ModelsPath, "models-ford-2013.json");
        MapJson(ComplaintsPath, "complaints-explorer-2013.json", model: "EXPLORER");
        MapJson(RecallsPath, "recalls-explorer-2013.json", model: "EXPLORER");
        _server.Given(Request.Create().WithPath(FlatFilePath).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(200).WithBody(BuildFlatFileZip(FlatFileRows)));
    }

    /// <summary>Makes the recalls feed fail for one model, to prove a mid-load failure stores nothing.</summary>
    public void FailRecallsFor(string model)
    {
        MapJson(ComplaintsPath, "complaints-explorer-2013.json", model);
        _server.Given(Request.Create().WithPath(RecallsPath).WithParam("model", model).UsingGet())
            .RespondWith(Response.Create().WithStatusCode(500).WithBody("upstream failure"));
    }

    /// <summary>Number of requests received so far, to prove nothing but GET was ever sent.</summary>
    public IReadOnlyList<string> ReceivedMethods => _server.LogEntries.Select(entry => entry.RequestMessage?.Method ?? string.Empty).ToList();

    public void Dispose() => _server.Dispose();

    /// <summary>Builds a zip whose single entry is a Latin-1 tab-separated file, as NHTSA publishes it.</summary>
    public static byte[] BuildFlatFileZip(IEnumerable<string> rows)
    {
        using var buffer = new MemoryStream();
        using (var archive = new ZipArchive(buffer, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("FLAT_INV.txt");
            using var writer = new StreamWriter(entry.Open(), Encoding.Latin1);
            foreach (var row in rows)
            {
                writer.WriteLine(row);
            }
        }

        return buffer.ToArray();
    }

    private void MapJson(string path, string fixtureFile, string? model = null)
    {
        var request = Request.Create().WithPath(path).UsingGet();
        if (model is not null)
        {
            request = request.WithParam("model", model);
        }

        _server.Given(request).RespondWith(Response.Create()
            .WithStatusCode(200)
            .WithHeader("Content-Type", JsonContentType)
            .WithBody(File.ReadAllText(Path.Combine(FixtureDirectory, fixtureFile))));
    }
}
