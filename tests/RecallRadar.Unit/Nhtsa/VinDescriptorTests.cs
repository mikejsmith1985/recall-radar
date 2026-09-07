// Checks the part of a VIN that says which version of a model it is.
using RecallRadar.Ingest.Nhtsa;

namespace RecallRadar.Unit.Nhtsa;

public sealed class VinDescriptorTests
{
    [Fact]
    public void TakesTheEightCharactersThatIdentifyTheVersion()
    {
        Assert.Equal("1FTFW1RJ", VinDescriptor.From("1FTFW1RJ0PFB00000"));
    }

    [Fact]
    public void CollapsesTheCheckDigitAndPlantThatDifferBetweenIdenticalTrucks()
    {
        // Two Raptor Rs off different lines share a descriptor and nothing after it. Cutting here
        // is what turns three hundred distinct strings into about fifty.
        Assert.Equal(VinDescriptor.From("1FTFW1RJ6PF"), VinDescriptor.From("1FTFW1RJ9PK"));
    }

    [Fact]
    public void UpperCasesSoTwoSpellingsAreOneDescriptor()
    {
        Assert.Equal("1FTFW1RJ", VinDescriptor.From(" 1ftfw1rj0pfb00000 "));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("1FTFW1R")]
    public void GivesNothingBackWhenThereIsNotEnoughVin(string? vin)
    {
        // A complaint with no VIN is an ordinary complaint, not an error.
        Assert.Null(VinDescriptor.From(vin));
    }
}
