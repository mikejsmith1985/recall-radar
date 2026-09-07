// Checks a record is only excluded when its trim or engine actually disagrees with the vehicle's.
using RecallRadar.Domain.Vehicles;

namespace RecallRadar.Unit.Vehicles;

public sealed class VehicleFitTests
{
    private static readonly VehicleFit RaptorR = VehicleFit.Create("SuperCrew-Raptor", 5.2m, 8);

    [Fact]
    public void ARecordFromTheSameTrimAndEngineBelongs()
    {
        Assert.True(VehicleFit.Create("SuperCrew-Raptor", 5.2m, 8).CouldBe(RaptorR));
    }

    [Fact]
    public void ARecordFromADifferentEngineDoesNot()
    {
        // The whole point: a 2.7 V6's transmission complaint is not about a supercharged V8.
        Assert.False(VehicleFit.Create("SuperCrew", 2.7m, 6).CouldBe(RaptorR));
    }

    [Fact]
    public void TheSameTrimWithADifferentEngineDoesNot()
    {
        // The 3.5 EcoBoost Raptor and the 5.2 Raptor R share a trim name and no engine.
        Assert.False(VehicleFit.Create("SuperCrew-Raptor", 3.5m, 6).CouldBe(RaptorR));
    }

    [Fact]
    public void ARecordNhtsaCouldNotDecodeIsKept()
    {
        // A quarter of the corpus decodes to blanks. Discarding it over a missing field would lose
        // more than the filter saves.
        Assert.True(VehicleFit.Unknown.CouldBe(RaptorR));
        Assert.True(VehicleFit.Create(null, 5.2m, null).CouldBe(RaptorR));
    }

    [Fact]
    public void AVehicleWithNothingDecodedExcludesNothing()
    {
        Assert.True(VehicleFit.Create("SuperCrew", 2.7m, 6).CouldBe(VehicleFit.Unknown));
    }

    [Theory]
    [InlineData("SuperCrew-Raptor", "supercrew raptor")]
    [InlineData("Raptor SVT", "RAPTOR-SVT")]
    public void PunctuationAndCasingAreNotTwoDifferentTrims(string mine, string theirs)
    {
        // NHTSA writes the same trim differently between model years.
        Assert.True(VehicleFit.Create(mine, null, null).CouldBe(VehicleFit.Create(theirs, null, null)));
    }

    [Fact]
    public void BlanksAndZeroesAreNotKnowledge()
    {
        var fit = VehicleFit.Create("   ", 0m, 0);

        Assert.False(fit.IsKnown);
        Assert.Equal(VehicleFit.Unknown, fit);
    }

    [Fact]
    public void DescribeReadsAsALabel()
    {
        Assert.Equal("SuperCrew-Raptor · 5.2L · 8 cyl", RaptorR.Describe());
        Assert.Equal("trim not decoded", VehicleFit.Unknown.Describe());
        Assert.Equal("5L · 8 cyl", VehicleFit.Create(null, 5.0m, 8).Describe());
    }
}
