// Checks how record text is cut into passages: whole for short records, paragraph-bounded for long ones.
using RecallRadar.Domain.Records;

namespace RecallRadar.Unit.Records;

public sealed class PassageChunkerTests
{
    [Fact]
    public void AsSinglePassage_ReturnsTheWholeBodyAsOrdinalZero()
    {
        const string body = "Exhaust odor enters the cabin.\n\nIt is worse when accelerating.";

        var passages = PassageChunker.AsSinglePassage(body);

        var only = Assert.Single(passages);
        Assert.Equal(0, only.Ordinal);
        Assert.Equal(body, only.Text);
    }

    [Fact]
    public void SplitIntoPassages_PacksWholeParagraphsUpToTheLimit()
    {
        var paragraphs = Enumerable.Range(1, 6).Select(index => new string((char)('a' + index), 400)).ToList();
        var body = string.Join("\r\n\r\n", paragraphs);

        var passages = PassageChunker.SplitIntoPassages(body, maxCharacters: 1000);

        // 400 + 2 + 400 = 802 fits; adding a third (1204) does not, so paragraphs pair up.
        Assert.Equal(3, passages.Count);
        Assert.Equal([0, 1, 2], passages.Select(passage => passage.Ordinal));
        Assert.All(passages, passage => Assert.True(passage.Text.Length <= 1000));
        Assert.Equal(paragraphs[0] + "\n\n" + paragraphs[1], passages[0].Text);
    }

    [Fact]
    public void SplitIntoPassages_BreaksAnOversizedParagraphAtSentenceEnds()
    {
        var sentences = Enumerable.Range(1, 5).Select(index => $"Sentence number {index} says something about the exhaust.").ToList();
        var body = string.Join(" ", sentences);

        var passages = PassageChunker.SplitIntoPassages(body, maxCharacters: 120);

        Assert.True(passages.Count > 1);
        Assert.All(passages, passage => Assert.True(passage.Text.Length <= 120));
        Assert.All(passages, passage => Assert.EndsWith(".", passage.Text));
        Assert.Equal(body, string.Join(" ", passages.Select(passage => passage.Text)));
    }

    [Fact]
    public void SplitIntoPassages_CutsASentenceLongerThanTheLimit()
    {
        var body = new string('x', 250);

        var passages = PassageChunker.SplitIntoPassages(body, maxCharacters: 100);

        Assert.Equal(3, passages.Count);
        Assert.Equal(100, passages[0].Text.Length);
        Assert.Equal(50, passages[2].Text.Length);
    }

    [Fact]
    public void SplitIntoPassages_JoinsWrappedLinesWithinAParagraph()
    {
        const string body = "First line of the paragraph\nsecond line of the same paragraph.\n\nNext paragraph.";

        var passages = PassageChunker.SplitIntoPassages(body);

        var only = Assert.Single(passages);
        Assert.Equal("First line of the paragraph second line of the same paragraph.\n\nNext paragraph.", only.Text);
    }

    [Fact]
    public void SplitIntoPassages_RejectsBlankBodyAndNonPositiveLimit()
    {
        Assert.Throws<ArgumentException>(() => PassageChunker.SplitIntoPassages("  "));
        Assert.Throws<ArgumentOutOfRangeException>(() => PassageChunker.SplitIntoPassages("text", maxCharacters: 0));
    }
}
