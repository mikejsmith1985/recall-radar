// Checks the scripted model drives both halves of verification: one quote survives, one is dropped.
using Microsoft.EntityFrameworkCore;
using RecallRadar.Api.Answering;
using RecallRadar.Api.Fixtures;
using RecallRadar.Domain.Grounding;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Integration.Fixtures;

[Collection(PostgresCollection.Name)]
public sealed class ScriptedAnswerModelTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string Body = "A STRONG EXHAUST ODOR ENTERS THE CABIN WHEN ACCELERATING HARD.";

    private int _vehicleId;
    private long _documentId;

    public async ValueTask InitializeAsync()
    {
        await using var context = postgres.CreateContext();
        var vehicle = Vehicle.Create("FORD", $"SCRIPT-{Guid.NewGuid():N}", 2013, $"Scripted fixture {Guid.NewGuid():N}");
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _vehicleId = vehicle.Id;

        var document = SourceDocument.Create(
            SourceKind.Complaint, $"scripted-{Guid.NewGuid():N}", _vehicleId,
            "ENGINE AND ENGINE COOLING:EXHAUST SYSTEM", new DateOnly(2016, 5, 1), "Exhaust odor", Body, "{}");
        context.SourceDocuments.Add(document);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        _documentId = document.Id;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task ItOffersOneQuoteThatVerifiesAndOneThatDoesNot()
    {
        // The browser suite needs a grounded answer that still shows a dropped count, so the page
        // renders both states in a single run.
        await using var context = postgres.CreateContext();
        var reply = await AskAsync(context);

        var parsed = AnswerResponseParser.Parse(reply.Json);
        Assert.True(parsed.IsParsed);
        var bodies = new Dictionary<string, string> { [_documentId.ToString()] = Body };
        var check = CitationCheck.Run(parsed.Answer!.Citations, bodies);

        Assert.True(check.IsGrounded);
        Assert.Equal(1, check.VerifiedCount);
        Assert.Equal(1, check.DroppedCount);
    }

    [Fact]
    public async Task TheVerifiedQuoteLocatesItselfInsideTheRecord()
    {
        await using var context = postgres.CreateContext();
        var reply = await AskAsync(context);

        var parsed = AnswerResponseParser.Parse(reply.Json);
        var check = CitationCheck.Run(
            parsed.Answer!.Citations, new Dictionary<string, string> { [_documentId.ToString()] = Body });
        var verified = Assert.Single(check.Verified);

        Assert.Equal(UxFixtureSeeder.VerifiableQuote, Body[verified.StartOffset..verified.EndOffset]);
    }

    [Fact]
    public async Task ItReturnsNoCitationsWhenNothingRetrievedCarriesTheQuote()
    {
        // Better an honest ungrounded answer than a citation pointing at a record that cannot support it.
        await using var context = postgres.CreateContext();
        var unrelated = new PromptRecord("999999", SourceKind.Complaint, "x", null, "The rear hatch rattles.");

        var reply = await new ScriptedAnswerModel(context)
            .AskAsync("vehicle", "exhaust smell", [unrelated], TestContext.Current.CancellationToken);

        var parsed = AnswerResponseParser.Parse(reply.Json);
        Assert.True(parsed.IsParsed);
        Assert.Empty(parsed.Answer!.Citations);
    }

    [Fact]
    public async Task ItNeverCallsOutOfTheProcess()
    {
        // The whole point: a browser run costs nothing and produces the same answer every time.
        await using var context = postgres.CreateContext();

        var first = await AskAsync(context);
        var second = await AskAsync(context);

        Assert.Equal(first.Json, second.Json);
        Assert.Null(first.Refusal);
    }

    private async Task<ModelReply> AskAsync(RecallRadarDbContext context)
    {
        var record = new PromptRecord(
            _documentId.ToString(), SourceKind.Complaint, "scripted", new DateOnly(2016, 5, 1), Body);
        return await new ScriptedAnswerModel(context)
            .AskAsync(UxFixtureSeeder.VehicleDisplayName, "exhaust smell", [record], TestContext.Current.CancellationToken);
    }
}
