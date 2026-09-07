// Checks a remembered decode holds what vPIC said, including that it said nothing.
using RecallRadar.Domain.Vehicles;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Unit.Persistence;

public sealed class VinDecodeTests
{
    private static readonly DateTimeOffset DecodedAt = new(2026, 9, 7, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void RemembersTheTrimAndEngineADescriptorStandsFor()
    {
        var decode = VinDecode.Create("1FTFW1RJ", 2023, VehicleFit.Create("SuperCrew-Raptor", 5.2m, 8), DecodedAt);

        Assert.Equal("SuperCrew-Raptor", decode.Trim);
        Assert.Equal(5.2m, decode.EngineLitres);
        Assert.Equal(8, decode.EngineCylinders);
        Assert.Equal(DecodedAt, decode.DecodedAt);
    }

    [Fact]
    public void RoundTripsBackToTheFitItWasGiven()
    {
        var fit = VehicleFit.Create("SuperCrew-Raptor", 5.2m, 8);

        Assert.Equal(fit, VinDecode.Create("1FTFW1RJ", 2023, fit, DecodedAt).Fit);
    }

    [Fact]
    public void RemembersThatNhtsaKnewNothing()
    {
        // "vPIC does not know" is an answer. Asking again every load would cost the same requests
        // to learn the same nothing.
        var decode = VinDecode.Create("1FTMF1C5", 2023, VehicleFit.Unknown, DecodedAt);

        Assert.False(decode.Fit.IsKnown);
        Assert.Null(decode.Trim);
    }

    [Fact]
    public void UpperCasesTheDescriptorSoOneSpellingIsRemembered()
    {
        Assert.Equal("1FTFW1RJ", VinDecode.Create(" 1ftfw1rj ", 2023, VehicleFit.Unknown, DecodedAt).Descriptor);
    }

    [Fact]
    public void KeepsTheComparisonKeyBesideTheTrimItCameFrom()
    {
        var decode = VinDecode.Create("1FTFW1RJ", 2023, VehicleFit.Create("SuperCrew-Raptor", null, null), DecodedAt);

        Assert.Equal("supercrewraptor", decode.TrimKey);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void RefusesADescriptorThatIdentifiesNothing(string? descriptor)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => VinDecode.Create(descriptor!, 2023, VehicleFit.Unknown, DecodedAt));
    }
}
