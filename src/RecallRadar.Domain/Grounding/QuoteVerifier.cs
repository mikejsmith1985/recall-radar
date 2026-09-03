// Checks a model's quote against the record it names before the quote is allowed to count as evidence.
using System.Text;

namespace RecallRadar.Domain.Grounding;

/// <summary>The outcome of checking one quote against one record body.</summary>
/// <param name="IsVerified">True only when the quote appears in the body.</param>
/// <param name="Reason">Why verification failed, in plain language; empty when it succeeded.</param>
/// <param name="StartOffset">Where the match starts in the original body, or -1 when there is no match.</param>
/// <param name="EndOffset">Exclusive end of the match in the original body, or -1 when there is no match.</param>
public sealed record QuoteVerification(bool IsVerified, string Reason, int StartOffset, int EndOffset)
{
    public static QuoteVerification Failed(string reason) => new(false, reason, -1, -1);

    public static QuoteVerification Succeeded(int startOffset, int endOffset) => new(true, string.Empty, startOffset, endOffset);
}

/// <summary>
/// Constitution Article X in its most literal form: a citation is a claim until the quote it rests
/// on is found in the record it names. Only whitespace is normalised, because line wrapping differs
/// between how a record is stored and how a model echoes it back and that difference means nothing.
/// Any other difference is a paraphrase, and a paraphrase is exactly what must not pass.
/// </summary>
/// <remarks>
/// Comparison is ordinal and case-sensitive on purpose. Case-insensitive matching would let
/// "Recall" pass for "recall", which is harmless, but it would also open the door to the next
/// "harmless" relaxation. A verifier that bends is not a verifier.
/// </remarks>
public static class QuoteVerifier
{
    public const string EmptyQuoteReason = "The citation carries no quoted text, so there is nothing to check.";
    public const string NotFoundReason = "The quoted passage does not appear in the cited record. A claim that cannot be checked against the source is not evidence.";

    /// <summary>
    /// Collapses every run of whitespace to a single space and trims the ends. This is the only
    /// normalisation applied to either side of the comparison.
    /// </summary>
    public static string NormaliseWhitespace(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        var normalised = new StringBuilder(text.Length);
        var isPendingSpace = false;
        foreach (var character in text)
        {
            if (char.IsWhiteSpace(character))
            {
                isPendingSpace = normalised.Length > 0;
                continue;
            }

            if (isPendingSpace)
            {
                normalised.Append(' ');
                isPendingSpace = false;
            }

            normalised.Append(character);
        }

        return normalised.ToString();
    }

    /// <summary>Checks whether the quote appears verbatim (whitespace aside) in the body, and where.</summary>
    public static QuoteVerification Verify(string quote, string body)
    {
        ArgumentNullException.ThrowIfNull(quote);
        ArgumentNullException.ThrowIfNull(body);

        var normalisedQuote = NormaliseWhitespace(quote);
        if (normalisedQuote.Length == 0)
        {
            return QuoteVerification.Failed(EmptyQuoteReason);
        }

        var (normalisedBody, originalIndexes) = NormaliseWithIndexMap(body);
        var matchIndex = normalisedBody.IndexOf(normalisedQuote, StringComparison.Ordinal);
        if (matchIndex < 0)
        {
            return QuoteVerification.Failed(NotFoundReason);
        }

        var startOffset = originalIndexes[matchIndex];
        var endOffset = originalIndexes[matchIndex + normalisedQuote.Length - 1] + 1;
        return QuoteVerification.Succeeded(startOffset, endOffset);
    }

    /// <summary>
    /// Normalises the body while remembering, for every kept character, where it came from in the
    /// original. That map is what turns a match in normalised text into offsets a UI can highlight.
    /// </summary>
    private static (string Normalised, int[] OriginalIndexes) NormaliseWithIndexMap(string body)
    {
        var normalised = new StringBuilder(body.Length);
        var originalIndexes = new List<int>(body.Length);
        var pendingSpaceIndex = -1;
        for (var index = 0; index < body.Length; index++)
        {
            var character = body[index];
            if (char.IsWhiteSpace(character))
            {
                if (normalised.Length > 0 && pendingSpaceIndex < 0)
                {
                    pendingSpaceIndex = index;
                }

                continue;
            }

            if (pendingSpaceIndex >= 0)
            {
                normalised.Append(' ');
                originalIndexes.Add(pendingSpaceIndex);
                pendingSpaceIndex = -1;
            }

            normalised.Append(character);
            originalIndexes.Add(index);
        }

        return (normalised.ToString(), originalIndexes.ToArray());
    }
}
