// Loads one vehicle's NHTSA records: fetch everything first, then store it all in one transaction.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RecallRadar.Ingest.Config;
using RecallRadar.Ingest.Nhtsa;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Ingest;

/// <summary>
/// Orchestrates a load. Every network call happens before the transaction opens, so a feed that
/// fails part-way leaves the stored data exactly as it was (FR-015). Records already present are
/// left alone; only new ones are inserted, which keeps repeat loads idempotent (FR-003).
/// </summary>
public sealed class IngestService(
    RecallRadarDbContext database,
    NhtsaComplaintsClient complaintsClient,
    NhtsaRecallsClient recallsClient,
    NhtsaModelsClient modelsClient,
    NhtsaFlatFileClient flatFileClient,
    IOptions<IngestOptions> options,
    ILogger<IngestService> logger)
{
    /// <summary>Validates the registration against NHTSA, fetches, plans and stores. Returns the counts.</summary>
    public async Task<IngestReport> IngestAsync(VehicleRegistration registration, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(registration);
        registration.Validate();
        await EnsureModelIsKnownAsync(registration, cancellationToken);

        var fetched = await FetchAsync(registration, cancellationToken);
        var complaints = IngestPlanner.PlanComplaints(fetched.Complaints);
        var recalls = IngestPlanner.PlanRecalls(fetched.Recalls);
        var investigations = IngestPlanner.PlanInvestigations(fetched.InvestigationRows);

        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var vehicle = await UpsertVehicleAsync(registration, cancellationToken);
        var complaintOutcome = await StoreDocumentsAsync(vehicle.Id, SourceKind.Complaint, complaints, cancellationToken);
        var recallOutcome = await StoreDocumentsAsync(vehicle.Id, SourceKind.Recall, recalls, cancellationToken);
        var investigationOutcome = await StoreDocumentsAsync(vehicle.Id, SourceKind.Investigation, investigations, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new IngestReport
        {
            Vehicle = registration,
            ComplaintsFetched = fetched.Complaints.Count,
            ComplaintsSkippedEmpty = fetched.Complaints.Count(complaint => !complaint.HasSummary),
            ComplaintsNew = complaintOutcome.NewDocuments,
            ComplaintsUnchanged = complaints.Count - complaintOutcome.NewDocuments,
            RecallsFetched = fetched.Recalls.Count,
            RecallsNew = recallOutcome.NewDocuments,
            RecallsUnchanged = recalls.Count - recallOutcome.NewDocuments,
            InvestigationRows = fetched.InvestigationRows.Count,
            InvestigationsNew = investigationOutcome.NewDocuments,
            LinksCreated = investigationOutcome.Links,
            ChunksCreated = complaintOutcome.Chunks + recallOutcome.Chunks + investigationOutcome.Chunks,
            ChunksEmbedded = 0,
        };
    }

    private sealed record FetchedRecords(
        IReadOnlyList<NhtsaComplaint> Complaints,
        IReadOnlyList<NhtsaRecall> Recalls,
        IReadOnlyList<NhtsaInvestigationRow> InvestigationRows);

    private sealed record StoreOutcome(int NewDocuments, int Chunks, int Links);

    private async Task EnsureModelIsKnownAsync(VehicleRegistration registration, CancellationToken cancellationToken)
    {
        var knownModels = await modelsClient.GetModelNamesAsync(registration.Make, registration.ModelYear, cancellationToken);
        if (!knownModels.Contains(registration.NhtsaModel))
        {
            throw new InvalidOperationException(
                $"NHTSA has no complaint model named '{registration.NhtsaModel}' for {registration.Make} {registration.ModelYear}. " +
                $"Known names include: {string.Join(", ", knownModels.Order().Take(20))}.");
        }
    }

    private async Task<FetchedRecords> FetchAsync(VehicleRegistration registration, CancellationToken cancellationToken)
    {
        var complaints = await complaintsClient.GetComplaintsAsync(registration.Make, registration.NhtsaModel, registration.ModelYear, cancellationToken);
        var recalls = await recallsClient.GetRecallsAsync(registration.Make, registration.ResolveRecallModel(), registration.ModelYear, cancellationToken);
        var archive = await flatFileClient.DownloadAsync(new Uri(options.Value.InvestigationsFlatFileUrl, UriKind.Absolute), cancellationToken);
        var rows = InvestigationFlatFileParser.ParseZip(archive, [registration]);
        logger.LogInformation(
            "Fetched {Complaints} complaints, {Recalls} recalls and {Rows} investigation rows for {Vehicle}.",
            complaints.Count, recalls.Count, rows.Count, registration.DisplayName);
        return new FetchedRecords(complaints, recalls, rows);
    }

    private async Task<Vehicle> UpsertVehicleAsync(VehicleRegistration registration, CancellationToken cancellationToken)
    {
        var candidate = Vehicle.Create(registration.Make, registration.NhtsaModel, registration.ModelYear, registration.DisplayName);
        var existing = await database.Vehicles.SingleOrDefaultAsync(
            vehicle => vehicle.Make == candidate.Make && vehicle.NhtsaModel == candidate.NhtsaModel && vehicle.ModelYear == candidate.ModelYear,
            cancellationToken);
        if (existing is not null)
        {
            return existing;
        }

        database.Vehicles.Add(candidate);
        await database.SaveChangesAsync(cancellationToken);
        return candidate;
    }

    /// <summary>Inserts the documents not already stored for this vehicle and kind, then their passages and links.</summary>
    private async Task<StoreOutcome> StoreDocumentsAsync(int vehicleId, SourceKind kind, IReadOnlyList<PlannedDocument> planned, CancellationToken cancellationToken)
    {
        var existingIds = await database.SourceDocuments
            .Where(document => document.VehicleId == vehicleId && document.Kind == kind)
            .Select(document => document.ExternalId)
            .ToHashSetAsync(cancellationToken);
        var newDocuments = planned
            .Where(document => !existingIds.Contains(document.ExternalId))
            .Select(document => (Planned: document, Entity: ToEntity(vehicleId, document)))
            .ToList();
        database.SourceDocuments.AddRange(newDocuments.Select(pair => pair.Entity));
        await database.SaveChangesAsync(cancellationToken);

        var chunkCount = 0;
        var linkCount = 0;
        foreach (var (plannedDocument, entity) in newDocuments)
        {
            database.DocumentChunks.AddRange(plannedDocument.Passages.Select(passage => DocumentChunk.Create(entity.Id, passage.Ordinal, passage.Text)));
            database.InvestigationLinks.AddRange(plannedDocument.Links.Select(link =>
                InvestigationLink.Create(entity.Id, link.CampaignNumber, link.Component, link.OpenedOn, link.ClosedOn)));
            chunkCount += plannedDocument.Passages.Count;
            linkCount += plannedDocument.Links.Count;
        }

        await database.SaveChangesAsync(cancellationToken);
        return new StoreOutcome(newDocuments.Count, chunkCount, linkCount);
    }

    private static SourceDocument ToEntity(int vehicleId, PlannedDocument planned) => SourceDocument.Create(
        planned.Kind, planned.ExternalId, vehicleId, planned.Component, planned.FiledOn, planned.Title, planned.Body, planned.RawPayload);
}
