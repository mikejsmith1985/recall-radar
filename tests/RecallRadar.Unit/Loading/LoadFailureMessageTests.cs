// Checks a failed load says something the person who asked for the car can act on.
using System.Net;
using RecallRadar.Api.Loading;

namespace RecallRadar.Unit.Loading;

public sealed class LoadFailureMessageTests
{
    [Fact]
    public void A400NamesTheModelNameAsTheUsualCause()
    {
        // What reached a user was "Response status code does not indicate success: 400 (Bad
        // Request).", which offers nothing to do next.
        var message = LoadFailureMessage.Describe(
            new HttpRequestException("Response status code does not indicate success: 400 (Bad Request).", null, HttpStatusCode.BadRequest));

        Assert.Contains("model name", message);
        Assert.DoesNotContain("does not indicate success: 400 (Bad Request).) (", message);
    }

    [Fact]
    public void AServerErrorSaysItIsNotTheCallersFault()
    {
        var message = LoadFailureMessage.Describe(
            new HttpRequestException("boom", null, HttpStatusCode.InternalServerError));

        Assert.Contains("their end", message);
    }

    [Fact]
    public void NoStatusCodeMeansNothingAnsweredAtAll()
    {
        var message = LoadFailureMessage.Describe(new HttpRequestException("No such host is known."));

        Assert.Contains("could not be reached", message);
    }

    [Theory]
    [InlineData(typeof(TaskCanceledException))]
    [InlineData(typeof(TimeoutException))]
    public void ATimeoutSuggestsWaitingRatherThanFixingAnything(Type failureType)
    {
        var message = LoadFailureMessage.Describe((Exception)Activator.CreateInstance(failureType)!);

        Assert.Contains("did not answer in time", message);
    }

    [Fact]
    public void TheOriginalTextIsKeptSoADeveloperStillHasTheDetail()
    {
        var message = LoadFailureMessage.Describe(new InvalidOperationException("column overflow at row 7"));

        Assert.Contains("column overflow at row 7", message);
        Assert.Contains("stopped before it finished", message);
    }

    [Fact]
    public void AVeryLongDetailIsTrimmedRatherThanFillingTheColumn()
    {
        var message = LoadFailureMessage.Describe(new InvalidOperationException(new string('x', 900)));

        Assert.EndsWith("…)", message);
        Assert.True(message.Length < 500, $"message was {message.Length} characters");
    }

    [Fact]
    public void AnEmptyDetailStillReadsAsASentence()
    {
        var message = LoadFailureMessage.Describe(new InvalidOperationException("   "));

        Assert.Contains("no detail given", message);
    }
}
