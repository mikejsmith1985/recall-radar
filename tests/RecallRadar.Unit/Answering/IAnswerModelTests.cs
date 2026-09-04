// Checks the reply type that decides whether an answer path continues or stops.
using RecallRadar.Api.Answering;

namespace RecallRadar.Unit.Answering;

public sealed class IAnswerModelTests
{
    [Fact]
    public void Answered_CarriesJsonAndNoRefusal()
    {
        var reply = ModelReply.Answered("""{"answer": "yes"}""");

        Assert.NotNull(reply.Json);
        Assert.Null(reply.Refusal);
    }

    [Fact]
    public void Declined_CarriesTheReasonAndNoJson()
    {
        var reply = ModelReply.Declined("The model declined to answer this question.");

        Assert.Null(reply.Json);
        Assert.Equal("The model declined to answer this question.", reply.Refusal);
    }

    [Fact]
    public void ARefusalAndAnAnswerAreNeverBothPresent()
    {
        // The service branches on Refusal first. If both could be set, a declined reply would be
        // parsed as an answer, and a refusal would reach the reader as prose.
        Assert.Null(ModelReply.Answered("{}").Refusal);
        Assert.Null(ModelReply.Declined("no").Json);
    }

    [Fact]
    public async Task AnImplementationCanBeSubstitutedWithoutTouchingTheNetwork()
    {
        // The seam exists so the answer path is testable and a live call is never made by accident.
        IAnswerModel model = new StubModel();

        var reply = await model.AskAsync("2013 Explorer Sport", "exhaust?", [], CancellationToken.None);

        Assert.Equal("""{"answer": "stub"}""", reply.Json);
    }

    private sealed class StubModel : IAnswerModel
    {
        public Task<ModelReply> AskAsync(
            string vehicleName, string question, IReadOnlyList<PromptRecord> records, CancellationToken cancellationToken) =>
            Task.FromResult(ModelReply.Answered("""{"answer": "stub"}"""));
    }
}
