// Retrieves records, asks the model, then checks every quote before any of it reaches a reader.
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RecallRadar.Domain.Grounding;
using RecallRadar.Domain.Retrieval;
using RecallRadar.Retrieval.Embeddings;
using RecallRadar.Retrieval.Persistence;
using RecallRadar.Retrieval.Search;

namespace RecallRadar.Api.Answering;

/// <summary>The answer as the endpoint returns it, with the accounting that proves it was checked.</summary>
public sealed record AnswerOutcome(
    long? AnswerId,
    GroundedAnswer Answer,
    IReadOnlyList<VerifiedCitation> Citations,
    IReadOnlyDictionary<string, PromptRecord> RecordsById,
    int DroppedCitationCount,
    IReadOnlyList<long> RetrievedDocumentIds,
    IReadOnlyList<DroppedCitation> DroppedCitations);

/// <summary>
/// The whole grounded-answer path. Retrieval decides what the model may see, the model decides
/// what to say, and verification decides what survives. An answer whose citations all fail is
/// returned as not grounded rather than as prose without evidence.
/// </summary>
public sealed class AnswerService(
    RecallRadarDbContext database,
    HybridSearchService search,
    IAnswerModel model)
{
    /// <summary>How many retrieved records the model is shown. Enough for a pattern, few enough to stay cheap.</summary>
    public const int RecordsShownToModel = 12;

    /// <summary>Runs one question end to end and persists what was returned.</summary>
    public async Task<AnswerOutcome> AskAsync(
        int vehicleId, string question, RetrievalMode? requestedMode, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);

        var hits = await RetrieveAsync(vehicleId, question, requestedMode, cancellationToken);
        var records = await LoadRecordsAsync(hits, cancellationToken);
        if (records.Count == 0)
        {
            return await PersistAsync(
                vehicleId, question,
                GroundedAnswer.NotGrounded("No records for this vehicle matched the question, so there is nothing to cite."),
                [], [], hits, cancellationToken);
        }

        var reply = await model.AskAsync(
            await DescribeVehicleAsync(vehicleId, cancellationToken), question, records, cancellationToken);
        if (reply.Refusal is { } refusal)
        {
            return await PersistAsync(vehicleId, question, GroundedAnswer.NotGrounded(refusal), [], records, hits, cancellationToken);
        }

        var parsed = AnswerResponseParser.Parse(reply.Json);
        if (!parsed.IsParsed)
        {
            return await PersistAsync(vehicleId, question, GroundedAnswer.NotGrounded(parsed.Failure!), [], records, hits, cancellationToken);
        }

        var check = CitationCheck.Run(parsed.Answer!.Citations, AnswerPrompt.BuildBodyIndex(records));
        var grounded = GroundedAnswer.From(parsed.Answer.AnswerText, parsed.Answer.IsKnownPattern, check, parsed.Answer.LinkedCampaigns);
        return await PersistAsync(
            vehicleId, question, grounded, check.Verified, records, hits, cancellationToken, check.DroppedCount, check.Dropped);
    }

    /// <summary>
    /// Retrieves with the strongest method available. Hybrid needs embeddings, so a request for it
    /// falls back to keyword search rather than failing: an answer from keyword hits beats no answer.
    /// </summary>
    private async Task<IReadOnlyList<SearchHit>> RetrieveAsync(
        int vehicleId, string question, RetrievalMode? requestedMode, CancellationToken cancellationToken)
    {
        var mode = requestedMode ?? RetrievalMode.Hybrid;
        try
        {
            return await RunSearchAsync(vehicleId, question, mode, cancellationToken);
        }
        catch (Exception failure) when (failure is EmbeddingsUnavailableException or EmbeddingProviderUnavailableException
                                        && mode.RequiresEmbeddings())
        {
            return await RunSearchAsync(vehicleId, question, RetrievalMode.Sparse, cancellationToken);
        }
    }

    private async Task<IReadOnlyList<SearchHit>> RunSearchAsync(
        int vehicleId, string question, RetrievalMode mode, CancellationToken cancellationToken)
    {
        if (!SearchRequest.TryCreate(vehicleId, question, mode.ToString(), null, null, null, RecordsShownToModel, out var request, out var problem))
        {
            throw new ArgumentException(problem, nameof(question));
        }

        return await search.SearchAsync(request!, cancellationToken);
    }

    /// <summary>Loads the verbatim bodies of the retrieved records. One entry per record, not per chunk.</summary>
    private async Task<IReadOnlyList<PromptRecord>> LoadRecordsAsync(
        IReadOnlyList<SearchHit> hits, CancellationToken cancellationToken)
    {
        var documentIds = hits.Select(hit => hit.DocumentId).Distinct().ToList();
        if (documentIds.Count == 0)
        {
            return [];
        }

        var rows = await database.SourceDocuments.AsNoTracking()
            .Where(document => documentIds.Contains(document.Id))
            .Select(document => new { document.Id, document.Kind, document.ExternalId, document.FiledOn, document.Body })
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Select(row => new PromptRecord(
                row.Id.ToString(CultureInfo.InvariantCulture), row.Kind, row.ExternalId, row.FiledOn, row.Body)),
        ];
    }

    private async Task<string> DescribeVehicleAsync(int vehicleId, CancellationToken cancellationToken)
    {
        var name = await database.Vehicles.AsNoTracking()
            .Where(vehicle => vehicle.Id == vehicleId)
            .Select(vehicle => vehicle.DisplayName)
            .FirstOrDefaultAsync(cancellationToken);
        return name ?? throw new VehicleNotFoundException(vehicleId);
    }

    /// <summary>Stores the answer with its citation accounting, then returns what the endpoint renders.</summary>
    private async Task<AnswerOutcome> PersistAsync(
        int vehicleId,
        string question,
        GroundedAnswer grounded,
        IReadOnlyList<VerifiedCitation> verified,
        IReadOnlyList<PromptRecord> records,
        IReadOnlyList<SearchHit> hits,
        CancellationToken cancellationToken,
        int droppedCount = 0,
        IReadOnlyList<DroppedCitation>? dropped = null)
    {
        var stored = Answer.Create(
            vehicleId, question, JsonSerializer.Serialize(grounded), verified.Count, droppedCount, DateTimeOffset.UtcNow);
        database.Answers.Add(stored);
        await database.SaveChangesAsync(cancellationToken);

        return new AnswerOutcome(
            stored.Id,
            grounded,
            verified,
            records.ToDictionary(record => record.DocumentId, StringComparer.Ordinal),
            droppedCount,
            [.. hits.Select(hit => hit.DocumentId).Distinct()],
            dropped ?? []);
    }
}
