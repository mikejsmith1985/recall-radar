// A canned answer for the browser suite, so it never spends money and never varies run to run.
using System.Globalization;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RecallRadar.Api.Answering;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Api.Fixtures;

/// <summary>
/// Stands in for Claude when the application runs in the browser-suite environment.
/// </summary>
/// <remarks>
/// A browser test should check the user interface, not the model. Calling the real model would
/// make every run cost money, take a minute, and produce different citations each time, so the
/// suite could only assert vague things. This returns one quote that exists in the seeded records
/// and one that does not, which drives the whole verification path: the first is shown with its
/// offsets, the second is dropped and counted, and the page has both states to render.
/// </remarks>
public sealed class ScriptedAnswerModel(RecallRadarDbContext database) : IAnswerModel
{
    public const string AnswerText =
        "Yes. Several owners of this vehicle report a strong exhaust smell entering the cabin under hard "
        + "acceleration, and NHTSA opened an investigation into it that led to a recall.";

    public async Task<ModelReply> AskAsync(
        string vehicleName, string question, IReadOnlyList<PromptRecord> records, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(records);

        var citingRecord = await FindRecordCarryingTheQuoteAsync(records, cancellationToken);
        if (citingRecord is null)
        {
            // Nothing retrieved holds the scripted quote, so the honest reply is an ungrounded one.
            return RecordedAnswer(AnswerText, []);
        }

        return RecordedAnswer(AnswerText,
        [
            (citingRecord, UxFixtureSeeder.VerifiableQuote),
            (citingRecord, UxFixtureSeeder.FabricatedQuote),
        ]);
    }

    /// <summary>Finds the retrieved record whose stored body really contains the scripted quote.</summary>
    private async Task<string?> FindRecordCarryingTheQuoteAsync(
        IReadOnlyList<PromptRecord> records, CancellationToken cancellationToken)
    {
        var byId = records.ToDictionary(record => record.DocumentId, StringComparer.Ordinal);
        var ids = byId.Keys
            .Select(id => long.TryParse(id, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) ? parsed : -1)
            .Where(id => id > 0)
            .ToList();

        var match = await database.SourceDocuments.AsNoTracking()
            .Where(document => ids.Contains(document.Id) && document.Body.Contains(UxFixtureSeeder.VerifiableQuote))
            .Select(document => document.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return match == 0 ? null : match.ToString(CultureInfo.InvariantCulture);
    }

    private static ModelReply RecordedAnswer(string answerText, IReadOnlyList<(string DocumentId, string Quote)> citations)
    {
        var payload = new Dictionary<string, object?>
        {
            [AnswerSchema.AnswerProperty] = answerText,
            [AnswerSchema.IsKnownPatternProperty] = citations.Count > 0,
            [AnswerSchema.CitationsProperty] = citations
                .Select(citation => new Dictionary<string, string>
                {
                    [AnswerSchema.DocumentIdProperty] = citation.DocumentId,
                    [AnswerSchema.QuoteProperty] = citation.Quote,
                })
                .ToList(),
            [AnswerSchema.LinkedCampaignsProperty] = new[] { UxFixtureSeeder.CampaignNumber },
        };

        return ModelReply.Answered(JsonSerializer.Serialize(payload));
    }
}
