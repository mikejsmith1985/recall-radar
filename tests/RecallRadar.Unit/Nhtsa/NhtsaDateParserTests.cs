// Checks the date formats NHTSA has actually been seen to use.
using RecallRadar.Ingest.Nhtsa;

namespace RecallRadar.Unit.Nhtsa;

public sealed class NhtsaDateParserTests
{
    [Theory]
    [InlineData("10/06/2019", 2019, 10, 6)]
    [InlineData("08/31/2026", 2026, 8, 31)]
    [InlineData("1/2/2019", 2019, 1, 2)]
    public void ParseSlashDate_PrefersMonthFirst(string text, int year, int month, int day)
    {
        Assert.Equal(new DateOnly(year, month, day), NhtsaDateParser.ParseSlashDate(text));
    }

    [Theory]
    [InlineData("25/07/2017", 2017, 7, 25)]
    [InlineData("28/08/2017", 2017, 8, 28)]
    public void ParseSlashDate_FallsBackToDayFirstWhenMonthFirstIsImpossible(string text, int year, int month, int day)
    {
        Assert.Equal(new DateOnly(year, month, day), NhtsaDateParser.ParseSlashDate(text));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a date")]
    [InlineData("31/31/2017")]
    public void ParseSlashDate_ReturnsNullForUnreadableInput(string? text)
    {
        Assert.Null(NhtsaDateParser.ParseSlashDate(text));
    }

    [Fact]
    public void IsAmbiguousSlashDate_FlagsValuesThatReadBothWays()
    {
        Assert.True(NhtsaDateParser.IsAmbiguousSlashDate("10/06/2019"));
        Assert.False(NhtsaDateParser.IsAmbiguousSlashDate("25/07/2017"));
        Assert.False(NhtsaDateParser.IsAmbiguousSlashDate("06/06/2019"));
        Assert.False(NhtsaDateParser.IsAmbiguousSlashDate(""));
    }

    [Fact]
    public void ParseCompactDate_ReadsTheFlatFileForm()
    {
        Assert.Equal(new DateOnly(2017, 7, 27), NhtsaDateParser.ParseCompactDate("20170727"));
        Assert.Null(NhtsaDateParser.ParseCompactDate(""));
        Assert.Null(NhtsaDateParser.ParseCompactDate("2017-07-27"));
    }
}
