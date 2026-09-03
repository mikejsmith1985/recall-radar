// A citation whose quote was found verbatim in its record, with where it sits in the original text.
namespace RecallRadar.Domain.Grounding;

/// <summary>
/// The evidence form of a citation. The offsets point into the record's stored body exactly as
/// it was received from NHTSA, so the user interface can highlight the quoted passage in place.
/// </summary>
/// <param name="Citation">The citation that passed verification.</param>
/// <param name="StartOffset">Zero-based index in the original body where the matched passage begins.</param>
/// <param name="EndOffset">Exclusive zero-based index in the original body where the matched passage ends.</param>
public sealed record VerifiedCitation(Citation Citation, int StartOffset, int EndOffset)
{
    /// <summary>Number of characters of the original body the passage covers.</summary>
    public int Length => EndOffset - StartOffset;

    /// <summary>Returns the exact original text that was matched, taken from the body it was verified against.</summary>
    public string SliceOf(string body)
    {
        ArgumentNullException.ThrowIfNull(body);
        if (StartOffset < 0 || EndOffset > body.Length || StartOffset > EndOffset)
        {
            throw new ArgumentOutOfRangeException(nameof(body), "The offsets do not fit inside the supplied body.");
        }

        return body[StartOffset..EndOffset];
    }
}
