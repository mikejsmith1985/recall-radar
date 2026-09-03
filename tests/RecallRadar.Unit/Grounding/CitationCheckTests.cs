// Proves citation checking fails closed and keeps honest counts of what was dropped.
using RecallRadar.Domain.Grounding;

namespace RecallRadar.Unit.Grounding;

public sealed class CitationCheckTests
{
    private static readonly IReadOnlyDictionary<string, string> Bodies = new Dictionary<string, string>
    {
        ["complaint-1"] = "Exhaust odor entered the passenger cabin while driving.",
        ["recall-19V435000"] = "A fractured rear toe link will cause a sudden change in vehicle handling.",
    };

    [Fact]
    public void Run_KeepsVerifiedCitationsInEmittedOrderAndDropsTheRest()
    {
        var citations = new List<Citation>
        {
            new("recall-19V435000", "sudden change in vehicle handling"),
            new("complaint-1", "fumes in the cabin"),
            new("complaint-1", "Exhaust odor entered the passenger cabin"),
        };

        var check = CitationCheck.Run(citations, Bodies);

        Assert.Equal(["recall-19V435000", "complaint-1"], check.Verified.Select(verified => verified.Citation.DocumentId));
        Assert.Single(check.Dropped);
        Assert.Equal(QuoteVerifier.NotFoundReason, check.Dropped[0].Reason);
        Assert.Equal(2, check.VerifiedCount);
        Assert.Equal(1, check.DroppedCount);
        Assert.Equal(3, check.EmittedCount);
        Assert.True(check.IsGrounded);
    }

    [Fact]
    public void Run_DropsCitationToADocumentTheModelWasNotShown()
    {
        var citations = new List<Citation> { new("complaint-999", "Exhaust odor entered the passenger cabin") };

        var check = CitationCheck.Run(citations, Bodies);

        Assert.Empty(check.Verified);
        Assert.Equal(CitationCheck.UnknownDocumentReason, check.Dropped[0].Reason);
        Assert.False(check.IsGrounded);
    }

    [Fact]
    public void Run_DropsCitationWithNoDocumentId()
    {
        var citations = new List<Citation> { new(" ", "Exhaust odor entered the passenger cabin") };

        var check = CitationCheck.Run(citations, Bodies);

        Assert.Equal(CitationCheck.MissingDocumentIdReason, check.Dropped[0].Reason);
    }

    [Fact]
    public void Run_ZeroSurvivorsIsNotGroundedEvenWhenManyWereEmitted()
    {
        var citations = Enumerable.Range(0, 4)
            .Select(index => new Citation("complaint-1", $"invented passage {index}"))
            .ToList();

        var check = CitationCheck.Run(citations, Bodies);

        Assert.False(check.IsGrounded);
        Assert.Equal(4, check.DroppedCount);
    }

    [Fact]
    public void Run_NoCitationsAtAllIsNotGrounded()
    {
        var check = CitationCheck.Run([], Bodies);

        Assert.False(check.IsGrounded);
        Assert.Equal(0, check.EmittedCount);
    }

    [Fact]
    public void Run_VerifiedOffsetsSliceTheOriginalBody()
    {
        var check = CitationCheck.Run([new Citation("complaint-1", "passenger   cabin")], Bodies);

        var verified = Assert.Single(check.Verified);
        Assert.Equal("passenger cabin", verified.SliceOf(Bodies["complaint-1"]));
    }
}
