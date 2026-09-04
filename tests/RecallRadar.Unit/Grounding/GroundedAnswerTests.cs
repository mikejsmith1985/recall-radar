// Proves the answer's grounded flag and dropped count can only come from the citation check.
using RecallRadar.Domain.Grounding;

namespace RecallRadar.Unit.Grounding;

public sealed class GroundedAnswerTests
{
    private static readonly IReadOnlyDictionary<string, string> Bodies = new Dictionary<string, string>
    {
        ["inv-EA17002"] = "Exhaust Odor in Passenger Cab. The agency reviewed and analyzed complaints.",
    };

    [Fact]
    public void From_IsGroundedWhenACitationSurvivedAndDroppedCountEqualsEmittedMinusVerified()
    {
        var check = CitationCheck.Run(
            [new Citation("inv-EA17002", "Exhaust Odor in Passenger Cab."), new Citation("inv-EA17002", "made up")],
            Bodies);

        var answer = GroundedAnswer.From("Yes, this is a known pattern.", isKnownPattern: true, check, ["17V001000"]);

        Assert.True(answer.IsGrounded);
        Assert.True(answer.IsKnownPattern);
        Assert.Single(answer.VerifiedCitations);
        Assert.Equal(1, answer.DroppedCitationCount);
        Assert.Equal(["17V001000"], answer.LinkedCampaigns);
    }

    [Fact]
    public void From_DowngradesKnownPatternClaimWhenNothingWasVerified()
    {
        var check = CitationCheck.Run([new Citation("inv-EA17002", "made up")], Bodies);

        var answer = GroundedAnswer.From("Yes, definitely.", isKnownPattern: true, check, []);

        Assert.False(answer.IsGrounded);
        Assert.False(answer.IsKnownPattern);
        Assert.Empty(answer.VerifiedCitations);
        Assert.Equal(1, answer.DroppedCitationCount);
    }

    [Fact]
    public void NotGrounded_CarriesTheExplanationAndNoCitations()
    {
        var answer = GroundedAnswer.NotGrounded("The model declined to answer.");

        Assert.False(answer.IsGrounded);
        Assert.Equal("The model declined to answer.", answer.AnswerText);
        Assert.Empty(answer.VerifiedCitations);
        Assert.Equal(0, answer.DroppedCitationCount);
    }

    [Fact]
    public void From_DiscardsProseThatNoSurvivingEvidenceSupports()
    {
        // Text that reads as an answer will be read as one whatever flag sits beside it, so an
        // unsupported claim about a car's safety must not be returned at all.
        var allFailed = new CitationCheck([], [new DroppedCitation(new Citation("1", "invented"), "no match")]);

        var answer = GroundedAnswer.From("Yes, this is a known and dangerous fault.", true, allFailed, ["17V001000"]);

        Assert.False(answer.IsGrounded);
        Assert.False(answer.IsKnownPattern);
        Assert.DoesNotContain("dangerous fault", answer.AnswerText, StringComparison.Ordinal);
        Assert.Equal(GroundedAnswer.NoEvidenceSurvivedExplanation, answer.AnswerText);
        Assert.Empty(answer.LinkedCampaigns);
        Assert.Equal(1, answer.DroppedCitationCount);
    }

    [Fact]
    public void From_KeepsTheAnswerAndItsCampaignsWhenEvidenceSurvives()
    {
        var body = "A STRONG EXHAUST ODOR ENTERS THE CABIN.";
        var verified = QuoteVerifier.Verify("A STRONG EXHAUST ODOR", body);
        var check = new CitationCheck(
            [new VerifiedCitation(new Citation("1", "A STRONG EXHAUST ODOR"), verified.StartOffset, verified.EndOffset)],
            []);

        var answer = GroundedAnswer.From("Yes, other owners report it.", true, check, ["17V001000"]);

        Assert.True(answer.IsGrounded);
        Assert.True(answer.IsKnownPattern);
        Assert.Equal("Yes, other owners report it.", answer.AnswerText);
        Assert.Equal(["17V001000"], answer.LinkedCampaigns);
    }
}
