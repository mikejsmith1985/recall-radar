// The endpoints behind the question: ask one, and read the record a citation points at.
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RecallRadar.Api.Answering;
using RecallRadar.Domain.Retrieval;
using RecallRadar.Retrieval.Persistence;
using RecallRadar.Retrieval.Search;

namespace RecallRadar.Api.Endpoints;

/// <summary>
/// Maps POST /api/ask and GET /api/documents/{id}. A model failure is never a 500 here: the reader
/// gets an ungrounded answer that says what went wrong, because that is something they can act on.
/// </summary>
public static class AskEndpoints
{
    public const string NoAnsweringKeyDetail =
        "Answering is not configured on this server. Search still works, and every record is readable.";

    private const string NoAnsweringKeyTitle = "Answering unavailable";
    private const string UnknownVehicleTitle = "Unknown vehicle";
    private const string InvalidRequestTitle = "Invalid question";

    /// <summary>Registers the ask and document endpoints.</summary>
    public static IEndpointRouteBuilder MapAskEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);
        endpoints.MapPost("/api/ask", AskAsync);
        endpoints.MapGet("/api/documents/{id:long}", ReadDocumentAsync);
        return endpoints;
    }

    private static async Task<IResult> AskAsync(
        AskRequest request,
        [FromServices] IServiceProvider services,
        CancellationToken cancellationToken)
    {
        // Resolved rather than injected: the service is only registered when answering is possible,
        // and a nullable constructor parameter would be read as a second request body. Registration
        // is the authority, not the key: the browser-suite environment registers a scripted model
        // and has no key at all.
        var answers = services.GetService<AnswerService>();
        if (answers is null)
        {
            return Results.Problem(
                detail: NoAnsweringKeyDetail, statusCode: StatusCodes.Status503ServiceUnavailable, title: NoAnsweringKeyTitle);
        }

        if (request is null || string.IsNullOrWhiteSpace(request.Question))
        {
            return Results.Problem(
                detail: "A question is required.", statusCode: StatusCodes.Status400BadRequest, title: InvalidRequestTitle);
        }

        try
        {
            var outcome = await answers.AskAsync(
                request.VehicleId, request.Question, RetrievalModes.TryParse(request.Mode), cancellationToken);
            return Results.Ok(AskResponse.From(outcome));
        }
        catch (VehicleNotFoundException notFound)
        {
            return Results.Problem(
                detail: notFound.Message, statusCode: StatusCodes.Status404NotFound, title: UnknownVehicleTitle);
        }
        catch (ArgumentException invalid)
        {
            return Results.Problem(
                detail: invalid.Message, statusCode: StatusCodes.Status400BadRequest, title: InvalidRequestTitle);
        }
    }

    /// <summary>Returns one record verbatim, so the client can highlight a verified quote inside it.</summary>
    private static async Task<IResult> ReadDocumentAsync(
        long id, [FromServices] RecallRadarDbContext database, CancellationToken cancellationToken)
    {
        var document = await database.SourceDocuments.AsNoTracking()
            .Where(row => row.Id == id)
            .Select(row => new DocumentResponse(
                row.Id, row.Kind.ToString().ToLowerInvariant(), row.ExternalId, row.Title,
                row.Component, row.FiledOn, row.Body, row.VehicleId))
            .FirstOrDefaultAsync(cancellationToken);

        return document is null ? Results.NotFound() : Results.Ok(document);
    }
}

/// <summary>One question about one vehicle.</summary>
public sealed record AskRequest(int VehicleId, string Question, string? Mode);

/// <summary>A verified citation as the client renders it, with the offsets that locate it in the record.</summary>
public sealed record CitationResponse(
    long DocumentId, string ExternalId, string Kind, string Quote, int StartOffset, int EndOffset);

/// <summary>
/// A citation that failed verification, with the reason. Shown because a reader deserves to know
/// what was claimed and rejected, not only that something was.
/// </summary>
public sealed record DroppedCitationResponse(string DocumentId, string Quote, string Reason)
{
    /// <summary>Flattens a dropped citation for the wire.</summary>
    public static DroppedCitationResponse From(Domain.Grounding.DroppedCitation dropped)
    {
        ArgumentNullException.ThrowIfNull(dropped);
        return new DroppedCitationResponse(dropped.Citation.DocumentId, dropped.Citation.Quote, dropped.Reason);
    }
}

/// <summary>The answer, its surviving evidence, and what did not survive.</summary>
public sealed record AskResponse(
    long? AnswerId,
    string Answer,
    bool IsKnownPattern,
    bool IsGrounded,
    IReadOnlyList<CitationResponse> Citations,
    int DroppedCitationCount,
    IReadOnlyList<DroppedCitationResponse> DroppedCitations,
    IReadOnlyList<string> LinkedCampaigns,
    IReadOnlyList<long> RetrievedDocumentIds)
{
    /// <summary>Flattens the outcome into the wire shape from contracts/http-api.md.</summary>
    public static AskResponse From(AnswerOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        return new AskResponse(
            outcome.AnswerId,
            outcome.Answer.AnswerText,
            outcome.Answer.IsKnownPattern,
            outcome.Answer.IsGrounded,
            [.. outcome.Citations.Select(citation => Describe(citation, outcome))],
            outcome.DroppedCitationCount,
            [.. outcome.DroppedCitations.Select(DroppedCitationResponse.From)],
            outcome.Answer.LinkedCampaigns,
            outcome.RetrievedDocumentIds);
    }

    private static CitationResponse Describe(Domain.Grounding.VerifiedCitation citation, AnswerOutcome outcome)
    {
        var documentId = citation.Citation.DocumentId;
        var record = outcome.RecordsById.GetValueOrDefault(documentId);
        return new CitationResponse(
            long.TryParse(documentId, out var parsed) ? parsed : 0,
            record?.ExternalId ?? string.Empty,
            record?.Kind.ToString().ToLowerInvariant() ?? string.Empty,
            citation.Citation.Quote,
            citation.StartOffset,
            citation.EndOffset);
    }
}

/// <summary>One record, verbatim, for highlighting a quote against.</summary>
public sealed record DocumentResponse(
    long Id, string Kind, string ExternalId, string Title, string Component, DateOnly? FiledOn, string Body, int VehicleId);
