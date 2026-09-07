// Checks the VIN is read out of a stored complaint payload, whatever shape that payload is in.
using RecallRadar.Ingest.Commands;

namespace RecallRadar.Unit.Commands;

public sealed class DecodeCommandTests
{
    [Fact]
    public void ReadsTheVinNhtsaFilesWithAComplaint()
    {
        // Eleven characters: NHTSA strips the six-digit serial before publishing.
        var payload = """{"odiNumber":11761788,"vin":"1FTMF1C50PK","components":"POWER TRAIN"}""";

        Assert.Equal("1FTMF1C50PK", DecodeCommand.ReadVin(payload));
    }

    [Theory]
    [InlineData("""{"odiNumber":11760888,"vin":""}""")]
    [InlineData("""{"odiNumber":11760888}""")]
    public void GivesNothingBackWhenThereIsNoVin(string payload)
    {
        // A complaint with no VIN is an ordinary complaint, not an error.
        Assert.Null(DecodeCommand.ReadVin(payload));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("EA17002\tFORD\tEXPLORER")]
    [InlineData("{ not json at all")]
    public void GivesNothingBackForAPayloadThatIsNotAComplaint(string? payload)
    {
        // Investigations are stored as flat-file rows, and they run through the same pass.
        Assert.Null(DecodeCommand.ReadVin(payload));
    }

    [Fact]
    public void TrimsTheSurroundingSpaceNhtsaSometimesLeaves()
    {
        Assert.Equal("1FTMF1C50PK", DecodeCommand.ReadVin("""{"vin":"  1FTMF1C50PK  "}"""));
    }
}
