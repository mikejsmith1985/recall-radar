// Turns the date strings NHTSA uses, in the formats it has been seen to use, into dates.
using System.Globalization;

namespace RecallRadar.Ingest.Nhtsa;

/// <summary>
/// NHTSA's APIs document month-first dates (<c>MM/dd/yyyy</c>) but the recalls feed has returned
/// day-first ones (<c>25/07/2017</c>). The flat files use <c>yyyyMMdd</c>. Month-first is preferred
/// whenever it is a valid date, and a value that only works day-first is read that way.
/// </summary>
public static class NhtsaDateParser
{
    private static readonly string[] MonthFirstFormats = ["MM/dd/yyyy", "M/d/yyyy"];
    private static readonly string[] DayFirstFormats = ["dd/MM/yyyy", "d/M/yyyy"];
    private const string CompactFormat = "yyyyMMdd";

    /// <summary>Parses a slash-separated date, preferring month-first and falling back to day-first.</summary>
    public static DateOnly? ParseSlashDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        if (TryParse(text.Trim(), MonthFirstFormats, out var monthFirst))
        {
            return monthFirst;
        }

        return TryParse(text.Trim(), DayFirstFormats, out var dayFirst) ? dayFirst : null;
    }

    /// <summary>True when both readings are valid dates and disagree, so the caller can record the doubt.</summary>
    public static bool IsAmbiguousSlashDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var isMonthFirstValid = TryParse(text.Trim(), MonthFirstFormats, out var monthFirst);
        var isDayFirstValid = TryParse(text.Trim(), DayFirstFormats, out var dayFirst);
        return isMonthFirstValid && isDayFirstValid && monthFirst != dayFirst;
    }

    /// <summary>Parses the eight-digit <c>yyyyMMdd</c> form the flat files use; blank means no date.</summary>
    public static DateOnly? ParseCompactDate(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        return DateOnly.TryParseExact(text.Trim(), CompactFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed)
            ? parsed
            : null;
    }

    private static bool TryParse(string text, string[] formats, out DateOnly parsed) =>
        DateOnly.TryParseExact(text, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed);
}
