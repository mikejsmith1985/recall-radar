// Drives the whole answer path against a real database: retrieve, ask, verify, persist.
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using RecallRadar.Api.Answering;
using RecallRadar.Domain.Retrieval;
using RecallRadar.Retrieval.Embeddings;
using RecallRadar.Retrieval.Persistence;
using RecallRadar.Retrieval.Search;

namespace RecallRadar.Integration.Answering;

[Collection(PostgresCollection.Name)]
public sealed class AnswerServiceTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string Question = "exhaust odor in the cabin";
    private const string Component = "ENGINE AND ENGINE COOLING";
    private const string TrueQuote = "A STRONG EXHAUST ODOR";
    private const string FabricatedQuote = "a strong exhaust odour was definitely observed";
    private const string ComplaintBody = "A STRONG EXHAUST ODOR ENTERS THE CABIN WHEN ACCELERATING HARD.";
    private const string InvestigationBody = "During the investigation, the agency reviewed owner complaints about exhaust odor.";

    private int _vehicleId;
    private long _complaintId;
    private long _investigationId;

    public async ValueTask InitializeAsync()
    {
        await using var context = postgres.CreateContext();
        var vehicle = Vehicle.Create("FORD", $"ANSWER-{Guid.NewGuid():N}", 2013, $"Answer fixture {Guid.NewGuid():N}");
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _vehicleId = vehicle.Id;

        _complaintId = await SeedAsync(context, SourceKind.Complaint, "complaint-1", ComplaintBody);
        _investigationId = await SeedAsync(context, SourceKind.Investigation, "EA17002", InvestigationBody);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task AVerifiableQuoteSurvivesAndTheAnswerIsGrounded()
    {
        var model = new RecordedAnswerModel(RecordedAnswerModel.BuildReply(
            "Yes, this is reported by other owners.", true, [(_complaintId.ToString(), TrueQuote)], ["17V001000"]));

        var outcome = await AskAsync(model);

        Assert.True(outcome.Answer.IsGrounded);
        Assert.True(outcome.Answer.IsKnownPattern);
        var citation = Assert.Single(outcome.Citations);
        Assert.Equal(TrueQuote, citation.Citation.Quote);
        Assert.Equal(0, outcome.DroppedCitationCount);
        Assert.Equal(["17V001000"], outcome.Answer.LinkedCampaigns);
    }

    [Fact]
    public async Task AVerifiedCitationCarriesOffsetsThatLocateItInTheStoredBody()
    {
        var model = new RecordedAnswerModel(RecordedAnswerModel.BuildReply(
            "Yes.", true, [(_complaintId.ToString(), TrueQuote)]));

        var outcome = await AskAsync(model);

        var citation = Assert.Single(outcome.Citations);
        Assert.Equal(TrueQuote, ComplaintBody[citation.StartOffset..citation.EndOffset]);
    }

    [Fact]
    public async Task AFabricatedQuoteIsDroppedAndCounted()
    {
        var model = new RecordedAnswerModel(RecordedAnswerModel.BuildReply(
            "Yes.", true, [(_complaintId.ToString(), TrueQuote), (_complaintId.ToString(), FabricatedQuote)]));

        var outcome = await AskAsync(model);

        Assert.True(outcome.Answer.IsGrounded);
        Assert.Single(outcome.Citations);
        Assert.Equal(1, outcome.DroppedCitationCount);
    }

    [Fact]
    public async Task AnAnswerWhoseCitationsAllFailIsNotGrounded()
    {
        var model = new RecordedAnswerModel(RecordedAnswerModel.BuildReply(
            "Yes, definitely a known fault.", true, [(_complaintId.ToString(), FabricatedQuote)]));

        var outcome = await AskAsync(model);

        Assert.False(outcome.Answer.IsGrounded);
        Assert.Empty(outcome.Citations);
        Assert.Equal(1, outcome.DroppedCitationCount);
        // The unsupported claim must not survive as a confident answer.
        Assert.DoesNotContain("definitely a known fault", outcome.Answer.AnswerText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ACitationNamingARecordTheModelWasNeverShownIsDropped()
    {
        var model = new RecordedAnswerModel(RecordedAnswerModel.BuildReply(
            "Yes.", true, [("999999", TrueQuote)]));

        var outcome = await AskAsync(model);

        Assert.False(outcome.Answer.IsGrounded);
        Assert.Equal(1, outcome.DroppedCitationCount);
    }

    [Fact]
    public async Task ARefusalBecomesAnUngroundedAnswerCarryingTheReason()
    {
        var model = new RecordedAnswerModel(ModelReply.Declined("The model declined to answer this question."));

        var outcome = await AskAsync(model);

        Assert.False(outcome.Answer.IsGrounded);
        Assert.Contains("declined", outcome.Answer.AnswerText, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(outcome.Citations);
    }

    [Fact]
    public async Task AMalformedReplyBecomesAnUngroundedAnswerRatherThanAnException()
    {
        var model = new RecordedAnswerModel(ModelReply.Answered("this is not the json the schema asked for"));

        var outcome = await AskAsync(model);

        Assert.False(outcome.Answer.IsGrounded);
        Assert.Contains("JSON", outcome.Answer.AnswerText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task TheModelOnlySeesRecordsThatRetrievalReturned()
    {
        var model = new RecordedAnswerModel(RecordedAnswerModel.BuildReply("Yes.", true, [(_complaintId.ToString(), TrueQuote)]));

        await AskAsync(model);

        Assert.All(model.LastRecords, record => Assert.Contains(
            record.DocumentId, new[] { _complaintId.ToString(), _investigationId.ToString() }));
        Assert.Contains(ComplaintBody, model.LastUserMessage, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EveryAnswerIsStoredWithItsCitationAccounting()
    {
        var model = new RecordedAnswerModel(RecordedAnswerModel.BuildReply(
            "Yes.", true, [(_complaintId.ToString(), TrueQuote), (_complaintId.ToString(), FabricatedQuote)]));

        var outcome = await AskAsync(model);

        await using var context = postgres.CreateContext();
        var stored = await context.Answers.AsNoTracking().SingleAsync(row => row.Id == outcome.AnswerId, TestContext.Current.CancellationToken);
        Assert.Equal(1, stored.VerifiedCitationCount);
        Assert.Equal(1, stored.DroppedCitationCount);
        Assert.True(stored.IsGrounded);
        Assert.Equal(Question, stored.Question);
    }

    [Fact]
    public async Task AskingAboutAnUnknownVehicleIsRejectedBeforeTheModelIsCalled()
    {
        var model = new RecordedAnswerModel(RecordedAnswerModel.BuildReply("Yes.", true, []));

        await using var context = postgres.CreateContext();
        var service = BuildService(context, model);

        await Assert.ThrowsAsync<VehicleNotFoundException>(
            () => service.AskAsync(-1, Question, RetrievalMode.Sparse, TestContext.Current.CancellationToken));
        Assert.Equal(0, model.CallCount);
    }

    [Fact]
    public async Task AskingForHybridFallsBackToKeywordSearchWhenNothingIsEmbedded()
    {
        // Refusing to answer because embeddings are missing would be worse than answering from the
        // keyword hits that exist, so the service steps down instead of failing.
        var model = new RecordedAnswerModel(RecordedAnswerModel.BuildReply("Yes.", true, [(_complaintId.ToString(), TrueQuote)]));

        await using var context = postgres.CreateContext();
        var outcome = await BuildService(context, model, new NullEmbeddingGenerator())
            .AskAsync(_vehicleId, Question, RetrievalMode.Hybrid, TestContext.Current.CancellationToken);

        Assert.True(outcome.Answer.IsGrounded);
        Assert.Equal(1, model.CallCount);
    }

    [Fact]
    public async Task TheCampaignRecordIsStillShownWhenComplaintsFillEveryPlace()
    {
        // The bug this pool exists to fix: one vehicle has thousands of complaints and a handful of
        // recalls, so a single ranking gives every place to complaints and the official record --
        // the thing the owner actually asked about -- is never shown to the model at all.
        await using var context = postgres.CreateContext();
        await SeedCrowdingComplaintsAsync(context, AnswerService.RecordsShownToModel * 2);
        var model = new RecordedAnswerModel(RecordedAnswerModel.BuildReply("Yes.", true, []));

        var outcome = await BuildService(context, model)
            .AskAsync(_vehicleId, Question, RetrievalMode.Sparse, TestContext.Current.CancellationToken);

        Assert.Contains(_investigationId.ToString(), model.LastRecords.Select(record => record.DocumentId));
        Assert.Contains(_investigationId, outcome.CampaignDocumentIds);
    }

    [Fact]
    public async Task TheCampaignPoolNeverContributesAComplaint()
    {
        await using var context = postgres.CreateContext();
        await SeedCrowdingComplaintsAsync(context, AnswerService.RecordsShownToModel * 2);
        var model = new RecordedAnswerModel(RecordedAnswerModel.BuildReply("Yes.", true, []));

        var outcome = await BuildService(context, model)
            .AskAsync(_vehicleId, Question, RetrievalMode.Sparse, TestContext.Current.CancellationToken);

        var kindsById = await context.SourceDocuments.AsNoTracking()
            .Where(document => outcome.CampaignDocumentIds.Contains(document.Id))
            .Select(document => document.Kind)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.NotEmpty(kindsById);
        Assert.DoesNotContain(SourceKind.Complaint, kindsById);
    }

    [Fact]
    public async Task TheTwoPoolsAreMergedWithoutShowingTheSameRecordTwice()
    {
        // The campaign hit can also be reachable in the wider pool; the model must not be handed
        // the same record under two entries, which would let one quote be counted twice.
        var model = new RecordedAnswerModel(RecordedAnswerModel.BuildReply("Yes.", true, []));

        await AskAsync(model);

        var documentIds = model.LastRecords.Select(record => record.DocumentId).ToList();
        Assert.Equal(documentIds.Count, documentIds.Distinct(StringComparer.Ordinal).Count());
    }

    /// <summary>Adds complaints that all match the question, so they compete for every place.</summary>
    private async Task SeedCrowdingComplaintsAsync(RecallRadarDbContext context, int count)
    {
        for (var index = 0; index < count; index++)
        {
            await SeedAsync(
                context, SourceKind.Complaint, $"crowd-{index}",
                $"{ComplaintBody} EXHAUST ODOR CABIN REPORT NUMBER {index}.");
        }
    }

    private async Task<AnswerOutcome> AskAsync(RecordedAnswerModel model)
    {
        await using var context = postgres.CreateContext();
        return await BuildService(context, model).AskAsync(_vehicleId, Question, RetrievalMode.Sparse, TestContext.Current.CancellationToken);
    }

    private static AnswerService BuildService(
        RecallRadarDbContext context, IAnswerModel model, IEmbeddingGenerator<string, Embedding<float>>? generator = null) =>
        new(context, new HybridSearchService(context, generator ?? new DeterministicEmbeddingGenerator()), model);

    private async Task<long> SeedAsync(RecallRadarDbContext context, SourceKind kind, string externalId, string body)
    {
        var document = SourceDocument.Create(
            kind, $"{externalId}-{Guid.NewGuid():N}", _vehicleId, Component, new DateOnly(2016, 5, 1), externalId, body, "{}");
        context.SourceDocuments.Add(document);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.DocumentChunks.Add(DocumentChunk.Create(document.Id, 0, body));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return document.Id;
    }
}
