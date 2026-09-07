// The settings that make a hosted API behave like a machine with no API keys.
using Microsoft.AspNetCore.Hosting;
using RecallRadar.Api.Config;

namespace RecallRadar.Integration;

/// <summary>
/// Says out loud that a test host has no embedding or answering key.
/// </summary>
/// <remarks>
/// Without this, whether a test passes depends on what the developer's shell happens to hold: a
/// vault injection an hour earlier leaves VOYAGE_API_KEY exported, and a test asserting that
/// embeddings are unavailable fails on that machine and nowhere else. An empty configured value
/// is an answer, not a gap, so it beats whatever the environment is carrying.
/// </remarks>
public static class NoKeys
{
    /// <summary>Applies the empty-key settings to a host under test.</summary>
    public static IWebHostBuilder WithNoKeys(this IWebHostBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.UseSetting(AppSettings.VoyageKeyVariable, string.Empty);
        builder.UseSetting(AppSettings.AnthropicKeyVariable, string.Empty);
        return builder;
    }
}
