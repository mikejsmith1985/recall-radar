// Checks which stored record kinds each retrieval scope admits.
using RecallRadar.Domain.Retrieval;
using RecallRadar.Retrieval.Persistence;
using RecallRadar.Retrieval.Search;

namespace RecallRadar.Unit.Search;

public sealed class ScopedKindsTests
{
    [Fact]
    public void TheCampaignScopeIsRecallsAndInvestigations()
    {
        // A campaign is the official record of a defect: the investigation that found it and the
        // recall that fixes it. Complaints are owners reporting symptoms, which is a different pool.
        Assert.Equal([SourceKind.Recall, SourceKind.Investigation], ScopedKinds.Of(RetrievalScope.Campaigns));
    }

    [Fact]
    public void TheAllScopeNamesEveryKindThatExists()
    {
        // If a fourth kind is ever ingested this fails, so someone must decide which pool it joins.
        Assert.Equal(Enum.GetValues<SourceKind>().Order(), ScopedKinds.Of(RetrievalScope.All).Order());
    }

    [Fact]
    public void OnlyTheAllScopeAdmitsComplaints()
    {
        Assert.Contains(SourceKind.Complaint, ScopedKinds.Of(RetrievalScope.All));
        Assert.DoesNotContain(SourceKind.Complaint, ScopedKinds.Of(RetrievalScope.Campaigns));
    }

    [Fact]
    public void TheCampaignScopeIsNarrowerThanEverything()
    {
        // The whole point of the second pool: fewer records compete, so a rare one can be seen.
        Assert.True(ScopedKinds.Of(RetrievalScope.Campaigns).Count < ScopedKinds.Of(RetrievalScope.All).Count);
    }

    [Fact]
    public void AnUnrecognisedScopeAdmitsEverythingRatherThanNothing()
    {
        // A future scope that reaches here unhandled must not silently return an empty result set,
        // which would read as "this vehicle has no records".
        Assert.NotEmpty(ScopedKinds.Of((RetrievalScope)99));
    }
}
