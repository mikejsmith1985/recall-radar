// Cuts record text into retrievable passages: one per short record, paragraph-bounded for long ones.
using System.Text;

namespace RecallRadar.Domain.Records;

/// <summary>
/// Splits text for retrieval. Complaints and recalls are short enough to search whole; investigation
/// summaries can run to many paragraphs, so they are packed into passages of at most
/// <see cref="DefaultMaxCharacters"/> characters without cutting a paragraph or sentence in half
/// unless a single sentence is itself longer than the limit.
/// </summary>
public static class PassageChunker
{
    public const int DefaultMaxCharacters = 1500;
    private const string ParagraphSeparator = "\n\n";
    private static readonly char[] SentenceTerminators = ['.', '!', '?'];

    /// <summary>Returns the whole body as the only passage, for records that are searched as a unit.</summary>
    public static IReadOnlyList<RetrievablePassage> AsSinglePassage(string body) =>
        [new RetrievablePassage(0, body)];

    /// <summary>Packs paragraphs into passages no longer than the limit, keeping their original order.</summary>
    public static IReadOnlyList<RetrievablePassage> SplitIntoPassages(string body, int maxCharacters = DefaultMaxCharacters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(body);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(maxCharacters, 0);

        var units = SplitParagraphs(body).SelectMany(paragraph => SplitOversized(paragraph, maxCharacters));
        var passages = new List<RetrievablePassage>();
        var current = new StringBuilder();
        foreach (var unit in units)
        {
            var wouldOverflow = current.Length > 0 && current.Length + ParagraphSeparator.Length + unit.Length > maxCharacters;
            if (wouldOverflow)
            {
                passages.Add(new RetrievablePassage(passages.Count, current.ToString()));
                current.Clear();
            }

            if (current.Length > 0)
            {
                current.Append(ParagraphSeparator);
            }

            current.Append(unit);
        }

        if (current.Length > 0)
        {
            passages.Add(new RetrievablePassage(passages.Count, current.ToString()));
        }

        return passages;
    }

    /// <summary>Paragraphs are separated by one or more blank lines; line-ending style does not matter.</summary>
    private static IEnumerable<string> SplitParagraphs(string body)
    {
        var normalised = body.Replace("\r\n", "\n").Replace('\r', '\n');
        var current = new StringBuilder();
        foreach (var line in normalised.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                if (current.Length > 0)
                {
                    yield return current.ToString().Trim();
                    current.Clear();
                }

                continue;
            }

            if (current.Length > 0)
            {
                current.Append(' ');
            }

            current.Append(line.Trim());
        }

        if (current.Length > 0)
        {
            yield return current.ToString().Trim();
        }
    }

    /// <summary>A paragraph over the limit is split at sentence ends; a sentence over the limit is cut at the limit.</summary>
    private static IEnumerable<string> SplitOversized(string paragraph, int maxCharacters)
    {
        if (paragraph.Length <= maxCharacters)
        {
            yield return paragraph;
            yield break;
        }

        var current = new StringBuilder();
        foreach (var sentence in SplitSentences(paragraph))
        {
            foreach (var piece in CutToLength(sentence, maxCharacters))
            {
                if (current.Length > 0 && current.Length + 1 + piece.Length > maxCharacters)
                {
                    yield return current.ToString();
                    current.Clear();
                }

                if (current.Length > 0)
                {
                    current.Append(' ');
                }

                current.Append(piece);
            }
        }

        if (current.Length > 0)
        {
            yield return current.ToString();
        }
    }

    private static IEnumerable<string> SplitSentences(string paragraph)
    {
        var start = 0;
        for (var index = 0; index < paragraph.Length - 1; index++)
        {
            var isSentenceEnd = SentenceTerminators.Contains(paragraph[index]) && char.IsWhiteSpace(paragraph[index + 1]);
            if (!isSentenceEnd)
            {
                continue;
            }

            yield return paragraph[start..(index + 1)].Trim();
            start = index + 1;
        }

        var tail = paragraph[start..].Trim();
        if (tail.Length > 0)
        {
            yield return tail;
        }
    }

    private static IEnumerable<string> CutToLength(string text, int maxCharacters)
    {
        for (var offset = 0; offset < text.Length; offset += maxCharacters)
        {
            yield return text.Substring(offset, Math.Min(maxCharacters, text.Length - offset));
        }
    }
}
