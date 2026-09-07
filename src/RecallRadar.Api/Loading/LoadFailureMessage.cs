// Turns the exception that stopped a load into something the person who asked for it can act on.
namespace RecallRadar.Api.Loading;

/// <summary>
/// Writes the sentence a failed load shows.
/// </summary>
/// <remarks>
/// A load fails in front of somebody who asked for a car by name, not in a log a developer reads.
/// "Response status code does not indicate success: 400 (Bad Request)." tells that person nothing
/// they can do, so the known shapes get a sentence naming which feed refused and what to try. The
/// original text is kept on the end, because the sentence is a translation, not a replacement, and
/// a developer still needs the detail.
/// </remarks>
public static class LoadFailureMessage
{
    /// <summary>Longest message worth keeping; the job column truncates anything past its own limit.</summary>
    public const int MaximumDetailLength = 300;

    private const string TimedOut = "NHTSA did not answer in time. The feeds are often slow rather than down, so try again in a few minutes.";
    private const string Unreachable = "NHTSA could not be reached. Check this machine's connection, then try again.";
    private const string Refused = "NHTSA refused the request for this vehicle. The model name is usually the reason: check it against the list the form offers.";
    private const string Unavailable = "NHTSA is not serving this feed right now. That is their end, not yours; try again later.";
    private const string Unexpected = "The load stopped before it finished.";

    /// <summary>The sentence to store on the job, with the original text kept as detail.</summary>
    public static string Describe(Exception failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        return $"{ChooseSentence(failure)} ({Shorten(failure.Message)})";
    }

    private static string ChooseSentence(Exception failure) => failure switch
    {
        TaskCanceledException or TimeoutException => TimedOut,
        HttpRequestException http => ChooseHttpSentence(http),
        _ => Unexpected,
    };

    private static string ChooseHttpSentence(HttpRequestException failure)
    {
        if (failure.StatusCode is null)
        {
            return Unreachable;
        }

        var status = (int)failure.StatusCode.Value;
        return status >= 500 ? Unavailable : Refused;
    }

    /// <summary>Keeps the detail readable; the whole message still has to fit a database column.</summary>
    private static string Shorten(string? detail)
    {
        var text = (detail ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return "no detail given";
        }

        return text.Length <= MaximumDetailLength ? text : text[..MaximumDetailLength] + "…";
    }
}
