// Checks the investigation window logic the evaluation harness relies on for ground truth.
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Unit.Persistence;

public sealed class InvestigationLinkTests
{
    private static readonly DateOnly Opened = new(2017, 7, 27);
    private static readonly DateOnly Closed = new(2023, 1, 17);

    [Fact]
    public void CoversDate_IsInclusiveOfBothEnds()
    {
        var link = InvestigationLink.Create(1, "17V001000", "STRUCTURE:BODY", Opened, Closed);

        Assert.True(link.CoversDate(Opened));
        Assert.True(link.CoversDate(Closed));
        Assert.True(link.CoversDate(new DateOnly(2019, 6, 1)));
        Assert.False(link.CoversDate(Opened.AddDays(-1)));
        Assert.False(link.CoversDate(Closed.AddDays(1)));
    }

    [Fact]
    public void CoversDate_OpenInvestigationCoversEverythingAfterItOpened()
    {
        var link = InvestigationLink.Create(1, "17V001000", "STRUCTURE:BODY", Opened, closedOn: null);

        Assert.True(link.CoversDate(new DateOnly(2099, 12, 31)));
        Assert.False(link.CoversDate(Opened.AddDays(-1)));
    }

    [Fact]
    public void Create_RejectsClosingBeforeOpening()
    {
        Assert.Throws<ArgumentException>(() =>
            InvestigationLink.Create(1, "17V001000", "STRUCTURE:BODY", Opened, Opened.AddDays(-1)));
    }

    [Fact]
    public void Create_RejectsMissingCampaignNumberBecauseItIsTheGroundTruthJoin()
    {
        Assert.Throws<ArgumentException>(() =>
            InvestigationLink.Create(1, " ", "STRUCTURE:BODY", Opened, Closed));
    }
}
