// Checks the invariants of a passage: ordered, non-empty, trimmed.
using RecallRadar.Domain.Records;

namespace RecallRadar.Unit.Records;

public sealed class RetrievablePassageTests
{
    [Fact]
    public void Constructor_TrimsSurroundingWhitespaceOnly()
    {
        var passage = new RetrievablePassage(2, "  inner  text \n");

        Assert.Equal(2, passage.Ordinal);
        Assert.Equal("inner  text", passage.Text);
    }

    [Fact]
    public void Constructor_RejectsNegativeOrdinalAndBlankText()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RetrievablePassage(-1, "text"));
        Assert.Throws<ArgumentException>(() => new RetrievablePassage(0, "   "));
    }
}
