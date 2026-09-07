// Seeds the throwaway database the browser suite runs against, so a green run means something.
using Microsoft.EntityFrameworkCore;
using RecallRadar.Retrieval.Evaluation;
using RecallRadar.Domain.Vehicles;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Api.Fixtures;

/// <summary>
/// Fills an empty database with the states the browser suite has to tell apart: a vehicle with
/// complaints, an investigation linked to a recall, and one recorded evaluation.
/// </summary>
/// <remarks>
/// The suite once passed every check against an empty page, which is why its support file refuses
/// to start without loaded data. This is what makes that check pass honestly. The text is real
/// wording from NHTSA complaints so the search and answer paths behave as they do in production,
/// but the set is small and fixed so a run is fast and its assertions are stable.
/// </remarks>
public static class UxFixtureSeeder
{
    public const string VehicleDisplayName = "2013 Explorer Sport";
    public const string Component = "ENGINE AND ENGINE COOLING:EXHAUST SYSTEM";
    public const string BrakeComponent = "SERVICE BRAKES, HYDRAULIC:FOUNDATION";
    public const string CampaignNumber = "17V001000";

    /// <summary>The quote the recorded answer cites. Present verbatim in the first complaint below.</summary>
    public const string VerifiableQuote = "A STRONG EXHAUST ODOR ENTERS THE CABIN";

    /// <summary>A quote the recorded answer also offers, which appears in no record, so it is dropped.</summary>
    public const string FabricatedQuote = "the manufacturer confirmed this is a known safety defect";

    /// <summary>The vehicle whose seeded load failed, so the suite can find that row specifically.</summary>
    public const string FailedLoadVehicleName = "2014 F-150 SVT Raptor";

    /// <summary>Why the seeded load failed. Shown in the table, so a silent failure is impossible.</summary>
    public const string FailedLoadMessage = "NHTSA returned 500 for the recalls feed.";

    /// <summary>
    /// A load seeded purely to be dismissed. The dismissal spec removes a row for good, so without
    /// one of its own it would take the failed load with it and every spec that runs afterwards
    /// would be testing a database the one before it had edited.
    /// </summary>
    public const string DismissableLoadVehicleName = "1998 Ranger (dismiss me)";

    /// <summary>
    /// The fixture vehicle carries a VIN so the browser suite can exercise the trim switch, which is
    /// hidden entirely without one. Not a real vehicle's number: the check digit is deliberately
    /// wrong, and nothing here ever reaches vPIC.
    /// </summary>
    public const string VehicleVin = "1FMHK8F83DGA00001";

    /// <summary>What the fixture vehicle's VIN stands for, and what most of its complaints share.</summary>
    public static readonly VehicleFit VehicleTrim = VehicleFit.Create("Sport", 3.5m, 6);

    /// <summary>One complaint belongs to another engine, so the narrow search has something to leave out.</summary>
    public const string OtherTrimExternalId = "11024727";
    public static readonly VehicleFit OtherTrim = VehicleFit.Create("Base", 2.0m, 4);

    private static readonly (string ExternalId, string Component, string Body)[] Complaints =
    [
        ("11257832", Component,
            "A STRONG EXHAUST ODOR ENTERS THE CABIN WHEN ACCELERATING HARD. IT HAPPENS EVERY TIME AND MY CHILDREN COMPLAIN OF HEADACHES."),
        ("10472937", Component,
            "WHEN ACCELERATING THE VEHICLE QUICKLY WITH THE AIR CONDITIONING ON, A STRONG SULFUR AND EXHAUST SMELL COMES THROUGH THE VENTS."),
        ("10954200", Component,
            "WHEN ACCELERATING RAPIDLY A STRONG EXHAUST ODOR IS PRESENT INSIDE THE PASSENGER COMPARTMENT. THE DEALER SAYS IT IS NORMAL."),
        ("10969178", Component,
            "I PURCHASED MY VEHICLE USED AND NOTICED AN EXHAUST SMELL IN THE CABIN, VERY STRONG DURING AND IMMEDIATELY AFTER HARD ACCELERATION."),
        ("11024727", BrakeComponent,
            "THE BRAKE PEDAL WENT TO THE FLOOR WITHOUT WARNING AND THE MASTER CYLINDER WAS FOUND TO BE LEAKING."),
    ];

    /// <summary>Seeds the fixture if, and only if, the database is empty. Re-running is a no-op.</summary>
    public static async Task SeedAsync(RecallRadarDbContext database, TimeProvider clock, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(database);
        ArgumentNullException.ThrowIfNull(clock);

        if (await database.Vehicles.AnyAsync(cancellationToken))
        {
            return;
        }

        var vehicle = Vehicle.Create("FORD", "EXPLORER", 2013, VehicleDisplayName, vin: VehicleVin);
        vehicle.DescribeFit(VehicleTrim);
        database.Vehicles.Add(vehicle);
        await database.SaveChangesAsync(cancellationToken);

        await SeedComplaintsAsync(database, vehicle.Id, cancellationToken);
        var investigationId = await SeedInvestigationAndRecallAsync(database, vehicle.Id, cancellationToken);
        await SeedInvestigationLinkAsync(database, investigationId, cancellationToken);
        await SeedEvaluationRunAsync(database, vehicle.Id, clock, cancellationToken);
        await SeedLoadsAsync(database, vehicle.Id, clock, cancellationToken);
    }

    private static async Task SeedComplaintsAsync(
        RecallRadarDbContext database, int vehicleId, CancellationToken cancellationToken)
    {
        var filedOn = new DateOnly(2016, 5, 1);
        foreach (var (externalId, component, body) in Complaints)
        {
            var documentId = await AddDocumentAsync(
                database, SourceKind.Complaint, externalId, vehicleId, component, filedOn, externalId, body, cancellationToken);
            var document = await database.SourceDocuments.SingleAsync(
                candidate => candidate.Id == documentId, cancellationToken);
            document.DescribeFit(
                "1FMHK8F8", externalId == OtherTrimExternalId ? OtherTrim : VehicleTrim);
            await database.SaveChangesAsync(cancellationToken);
            filedOn = filedOn.AddMonths(2);
        }
    }

    private static async Task<long> SeedInvestigationAndRecallAsync(
        RecallRadarDbContext database, int vehicleId, CancellationToken cancellationToken)
    {
        await AddDocumentAsync(
            database, SourceKind.Recall, CampaignNumber, vehicleId, Component, new DateOnly(2017, 3, 1),
            "Exhaust system sealing recall",
            "Ford is recalling certain vehicles. Exhaust gas may enter the passenger compartment. Dealers will reseal the rear of the body, free of charge.",
            cancellationToken);

        return await AddDocumentAsync(
            database, SourceKind.Investigation, "EA17002", vehicleId, Component, new DateOnly(2017, 7, 27),
            "Exhaust Odor in Passenger Cab",
            "During the EA17-002 investigation, the agency reviewed and analyzed complaints alleging exhaust odor in the passenger compartment during hard acceleration.",
            cancellationToken);
    }

    private static async Task SeedInvestigationLinkAsync(
        RecallRadarDbContext database, long investigationId, CancellationToken cancellationToken)
    {
        database.InvestigationLinks.Add(InvestigationLink.Create(
            investigationId, CampaignNumber, Component, new DateOnly(2017, 7, 27), new DateOnly(2023, 1, 17)));
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>One recorded run, so the evaluation page has a table rather than an empty state.</summary>
    private static async Task SeedEvaluationRunAsync(
        RecallRadarDbContext database, int vehicleId, TimeProvider clock, CancellationToken cancellationToken)
    {
        // The same shape EvaluationRunner writes: each mode maps to its numbers or to null, with
        // the reasons beside them. Dense and hybrid are null here because nothing is embedded yet,
        // which is the state the browser suite has to render. The campaign pool is present and
        // higher than the wider one, because telling the two apart is what the eval page is for.
        const string metrics =
            """
            {"sparse":{"recallAt5":0.075,"recallAt10":0.125,"mrr":0.057,"scoredCaseCount":80,"skippedCaseCount":0},
             "dense":null,
             "hybrid":null,
             "campaignsSparse":{"recallAt5":0.500,"recallAt10":0.625,"mrr":0.410,"scoredCaseCount":80,"skippedCaseCount":0},
             "campaignsDense":null,
             "campaignsHybrid":null,
             "skipped":{"dense":"No chunk for this vehicle has an embedding yet.","hybrid":"No chunk for this vehicle has an embedding yet.","campaignsDense":"No chunk for this vehicle has an embedding yet.","campaignsHybrid":"No chunk for this vehicle has an embedding yet."},
             "faithfulness":{"emitted":2,"verified":1}}
            """;

        database.EvaluationRuns.Add(EvaluationRun.Create(vehicleId, clock.GetUtcNow(), caseCount: 80, metrics));
        await database.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// One load that worked and one that did not, so the browser suite can tell them apart. The
    /// failure is a scheduled one: an overnight refresh failing silently is the case worth showing.
    /// </summary>
    private static async Task SeedLoadsAsync(
        RecallRadarDbContext database, int vehicleId, TimeProvider clock, CancellationToken cancellationToken)
    {
        var queuedAt = clock.GetUtcNow().AddHours(-2);

        var succeeded = IngestJob.Queue(
            "FORD", "EXPLORER", null, 2013, VehicleDisplayName, IngestTrigger.Manual, queuedAt);
        succeeded.Start(queuedAt.AddSeconds(1));
        succeeded.Succeed(
            vehicleId,
            """{"complaintsNew":5,"recallsNew":1,"investigationsNew":1,"chunksCreated":7,"chunksEmbedded":0}""",
            queuedAt.AddMinutes(3));

        var failed = IngestJob.Queue(
            "FORD", "EXPLORER", null, 2013, FailedLoadVehicleName, IngestTrigger.Scheduled, queuedAt.AddHours(1));
        failed.Start(queuedAt.AddHours(1).AddSeconds(1));
        failed.Fail(FailedLoadMessage, queuedAt.AddHours(1).AddSeconds(9));

        var dismissable = IngestJob.Queue(
            "FORD", "RANGER", null, 1998, DismissableLoadVehicleName, IngestTrigger.Manual, queuedAt.AddHours(2));
        dismissable.Start(queuedAt.AddHours(2).AddSeconds(1));
        dismissable.Fail("NHTSA returned 500 for the complaints feed.", queuedAt.AddHours(2).AddSeconds(4));

        database.IngestJobs.AddRange(succeeded, failed, dismissable);
        await database.SaveChangesAsync(cancellationToken);
    }

    private static async Task<long> AddDocumentAsync(
        RecallRadarDbContext database, SourceKind kind, string externalId, int vehicleId, string component,
        DateOnly filedOn, string title, string body, CancellationToken cancellationToken)
    {
        var document = SourceDocument.Create(kind, externalId, vehicleId, component, filedOn, title, body, "{}");
        database.SourceDocuments.Add(document);
        await database.SaveChangesAsync(cancellationToken);

        database.DocumentChunks.Add(DocumentChunk.Create(document.Id, 0, body));
        await database.SaveChangesAsync(cancellationToken);
        return document.Id;
    }
}
