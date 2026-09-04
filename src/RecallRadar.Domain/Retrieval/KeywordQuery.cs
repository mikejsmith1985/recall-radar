// Turns an owner's question into a keyword query that can actually match a record.
using System.Text;

namespace RecallRadar.Domain.Retrieval;

/// <summary>
/// Builds the text handed to PostgreSQL's full-text search.
/// </summary>
/// <remarks>
/// A question is not a search phrase. PostgreSQL's <c>websearch_to_tsquery</c> joins bare words
/// with AND, so "I get a strong exhaust smell inside the cabin when I accelerate" only matches a
/// record containing every one of those words, which no real record does. Joining the terms with
/// OR instead lets a record match on any of them, and <c>ts_rank_cd</c> then puts the records
/// covering the most terms first. That is the behaviour a reader expects from asking a question.
/// </remarks>
public static class KeywordQuery
{
    /// <summary>Words shorter than this carry no signal and only widen the match.</summary>
    public const int MinimumTermLength = 3;

    /// <summary>Enough terms to describe a symptom; more only slows the query down.</summary>
    public const int MaximumTerms = 24;

    private const char TermSeparator = '|';

    /// <summary>
    /// Words so common in this corpus, or in English, that matching them tells you nothing. Every
    /// record is a vehicle complaint, so "vehicle" and "car" are noise here in a way they are not
    /// elsewhere.
    /// </summary>
    private static readonly HashSet<string> IgnoredTerms = new(StringComparer.OrdinalIgnoreCase)
    {
        "the", "and", "but", "for", "was", "were", "are", "you", "your", "this", "that", "with",
        "from", "have", "has", "had", "not", "can", "will", "would", "could", "should", "when",
        "what", "why", "how", "does", "did", "get", "got", "any", "all", "its", "it's", "there",
        "then", "than", "know", "known", "problem", "issue", "vehicle", "car", "truck", "mine",
    };

    /// <summary>
    /// Builds an OR-joined tsquery from a question, or returns null when nothing usable remains.
    /// </summary>
    /// <remarks>
    /// Terms are reduced to letters and digits before use. A tsquery is parsed by the database, so
    /// a stray quote or ampersand from a question would otherwise be read as syntax.
    /// </remarks>
    public static string? BuildAnyTermQuery(string? question)
    {
        var terms = ExtractTerms(question);
        if (terms.Count == 0)
        {
            return null;
        }

        var builder = new StringBuilder();
        foreach (var term in terms)
        {
            if (builder.Length > 0)
            {
                builder.Append(' ').Append(TermSeparator).Append(' ');
            }

            builder.Append(term);
        }

        return builder.ToString();
    }

    /// <summary>The distinct, usable search terms in a question, in the order they were written.</summary>
    public static IReadOnlyList<string> ExtractTerms(string? question)
    {
        if (string.IsNullOrWhiteSpace(question))
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var terms = new List<string>();
        foreach (var word in question.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            var term = KeepLettersAndDigits(word);
            if (term.Length < MinimumTermLength || IgnoredTerms.Contains(term) || !seen.Add(term))
            {
                continue;
            }

            terms.Add(term);
            if (terms.Count == MaximumTerms)
            {
                break;
            }
        }

        return terms;
    }

    private static string KeepLettersAndDigits(string word)
    {
        var builder = new StringBuilder(word.Length);
        foreach (var character in word)
        {
            if (char.IsLetterOrDigit(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
