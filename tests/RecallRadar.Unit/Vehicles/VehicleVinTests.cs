// Checks what counts as a VIN, so the form and the endpoint cannot disagree about it.
using RecallRadar.Domain.Vehicles;

namespace RecallRadar.Unit.Vehicles;

public sealed class VehicleVinTests
{
    [Fact]
    public void AcceptsARealVin()
    {
        Assert.True(VehicleVin.IsWellFormed("1FTFW1RJ0PFB00000"));
    }

    [Theory]
    [InlineData("1FTFW1RJ6PFC9872")]
    [InlineData("1FTFW1RJ0PFB000000")]
    public void RefusesTheWrongLength(string vin)
    {
        // A half-typed VIN is worse than none: it would decode to a different truck.
        Assert.False(VehicleVin.IsWellFormed(vin));
    }

    [Theory]
    [InlineData("1FTFW1RJ6PFCI8720")]
    [InlineData("1FTFW1RJ6PFCO8720")]
    [InlineData("1FTFW1RJ6PFCQ8720")]
    public void RefusesTheLettersTheStandardLeavesOut(string vin)
    {
        // I, O and Q are excluded because they are read as 1 and 0.
        Assert.False(VehicleVin.IsWellFormed(vin));
    }

    [Fact]
    public void RefusesPunctuationInsideTheVin()
    {
        Assert.False(VehicleVin.IsWellFormed("1FTFW1RJ-PFC9872"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void BlankIsNotAVinAndIsNotAnError(string? vin)
    {
        Assert.False(VehicleVin.IsWellFormed(vin));
        Assert.Null(VehicleVin.Normalise(vin));
    }

    [Fact]
    public void NormalisesToUpperCaseWithoutSurroundingSpace()
    {
        Assert.Equal("1FTFW1RJ0PFB00000", VehicleVin.Normalise("  1ftfw1rj0pfb00000 "));
    }
}
