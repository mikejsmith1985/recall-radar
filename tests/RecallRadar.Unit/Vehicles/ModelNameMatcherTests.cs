// Checks that a rejected model name suggests the ones a person was plainly reaching for.
using RecallRadar.Domain.Vehicles;

namespace RecallRadar.Unit.Vehicles;

public sealed class ModelNameMatcherTests
{
    /// <summary>NHTSA's real names for Ford 2026, including the ones with repeated suffixes.</summary>
    private static readonly string[] Ford2026 =
    [
        "BRONCO 2DR ICE", "BRONCO 4DR ICE", "BRONCO SPORT ICE", "E-450", "ESCAPE GAS ICE",
        "ESCAPE PHEV PHEV", "EXPEDITION ICE", "EXPEDITION MAX ICE", "EXPLORER GAS ICE",
        "F-150 (REGULAR CAB) GAS ICE", "F-150 (SUPER CAB) GAS ICE", "F-150 (SUPER CREW) GAS ICE",
        "F-150 (SUPER CREW) HEV HEV", "F-250 (CREW CAB) ICE", "F-350 SD", "F-450 SD", "F-550 SD",
        "F-59", "F-600 SD", "F-650 SD", "MAVERICK HEV HEV", "MAVERICK ICE", "MUSTANG ICE",
        "MUSTANG MACH-E BEV BEV", "MUSTANG NO REAR SEAT ICE", "RANGER (SUPER CREW) ICE",
        "TRANSIT VAN GAS ICE",
    ];

    [Fact]
    public void TheNameSomebodyWasReachingForComesFirst()
    {
        // The bug this fixes: an alphabetical list stopped at F-59 and hid MUSTANG MACH-E entirely.
        var ranked = ModelNameMatcher.Rank("2026 Ford Mustang Mach-E GT", Ford2026, 5);

        Assert.Equal("MUSTANG MACH-E BEV BEV", ranked[0]);
    }

    [Fact]
    public void TheOtherPlausibleNamesFollowIt()
    {
        var ranked = ModelNameMatcher.Rank("Mustang Mach-E", Ford2026, 5);

        Assert.Contains("MUSTANG ICE", ranked);
        Assert.Contains("MUSTANG NO REAR SEAT ICE", ranked);
    }

    [Fact]
    public void ALongerMatchedWordOutweighsAShortOne()
    {
        // "E" alone matches E-450; MUSTANG MACH-E matches far more of what was typed.
        var ranked = ModelNameMatcher.Rank("Mustang Mach E", Ford2026, 3);

        Assert.Equal("MUSTANG MACH-E BEV BEV", ranked[0]);
        Assert.DoesNotContain("E-450", ranked);
    }

    [Fact]
    public void PunctuationAndCasingDoNotDecideTheOrder()
    {
        Assert.Equal(
            ModelNameMatcher.Rank("MACH-E", Ford2026, 3),
            ModelNameMatcher.Rank("  mach e  ", Ford2026, 3));
    }

    [Fact]
    public void ATruckNameFindsItsBodyStyles()
    {
        var ranked = ModelNameMatcher.Rank("F-150 Super Crew", Ford2026, 3);

        Assert.Contains("F-150 (SUPER CREW) GAS ICE", ranked);
        Assert.Contains("F-150 (SUPER CREW) HEV HEV", ranked);
    }

    [Fact]
    public void NothingResemblingTheInputFallsBackToAnAlphabeticalSample()
    {
        // Better a readable sample of what exists than an empty list that says nothing.
        var ranked = ModelNameMatcher.Rank("zzzzzz", Ford2026, 4);

        Assert.Equal(4, ranked.Count);
        Assert.Equal(ranked.Order(StringComparer.Ordinal), ranked);
    }

    [Fact]
    public void TheListNeverExceedsWhatWasAskedFor()
    {
        Assert.Equal(3, ModelNameMatcher.Rank("Mustang", Ford2026, 3).Count);
        Assert.Empty(ModelNameMatcher.Rank("Mustang", Ford2026, 0));
    }

    [Fact]
    public void AnEmptyOrAbsentQueryStillOffersSomething()
    {
        Assert.NotEmpty(ModelNameMatcher.Rank(null, Ford2026, 3));
        Assert.NotEmpty(ModelNameMatcher.Rank("   ", Ford2026, 3));
    }

    [Fact]
    public void NoCandidatesMeansNoSuggestions()
    {
        Assert.Empty(ModelNameMatcher.Rank("Mustang", [], 5));
    }

    [Fact]
    public void DuplicatesAreOfferedOnce()
    {
        // NHTSA returns the same name more than once for a model with several body styles.
        var ranked = ModelNameMatcher.Rank("Mustang", ["MUSTANG ICE", "MUSTANG ICE", "MUSTANG MACH-E BEV BEV"], 5);

        Assert.Equal(2, ranked.Count);
    }

    [Fact]
    public void TiesAreBrokenAlphabeticallySoTheOrderIsStable()
    {
        var first = ModelNameMatcher.Rank("Bronco", Ford2026, 3);
        var second = ModelNameMatcher.Rank("Bronco", Ford2026, 3);

        Assert.Equal(first, second);
        Assert.Equal(["BRONCO 2DR ICE", "BRONCO 4DR ICE", "BRONCO SPORT ICE"], first);
    }
}
