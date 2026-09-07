// Strongly-typed application settings that refuse to render secrets (Article IX).
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Api.Config;

/// <summary>
/// Everything the API needs from configuration. Secrets are held but never appear in
/// <see cref="ToString"/>, so an accidental log of the settings object leaks nothing.
/// </summary>
public sealed class AppSettings
{
    public const string AnthropicKeyVariable = "ANTHROPIC_API_KEY";
    public const string VoyageKeyVariable = "VOYAGE_API_KEY";

    /// <summary>Configuration key the integration host uses to point the API at a test container.</summary>
    public const string ConnectionConfigurationKey = "ConnectionStrings:RecallRadar";

    public string ConnectionString { get; init; } = string.Empty;
    public string? AnthropicApiKey { get; init; }
    public string? VoyageApiKey { get; init; }

    public bool HasConnectionString => !string.IsNullOrWhiteSpace(ConnectionString);
    public bool HasAnthropicKey => !string.IsNullOrWhiteSpace(AnthropicApiKey);
    public bool HasVoyageKey => !string.IsNullOrWhiteSpace(VoyageApiKey);

    /// <summary>
    /// Reads settings from the environment the vault or the developer's .env injects. The
    /// connection string may alternatively arrive through configuration, which is how the
    /// integration suite hands the API a throwaway container; the environment wins when both are set.
    /// </summary>
    public static AppSettings Load(string? configuredConnectionString, Func<string, string?> readEnvironment)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        return Load(configuredConnectionString, readEnvironment, _ => null);
    }

    /// <summary>
    /// Reads settings from the environment, falling back to configuration for anything unset.
    /// </summary>
    /// <remarks>
    /// Both sources are consulted because the rest of the application reads configuration, and
    /// health reports what is available from these settings. If the two disagreed, health could
    /// say a feature is unavailable while the service behind it was registered and working.
    /// </remarks>
    public static AppSettings Load(
        string? configuredConnectionString, Func<string, string?> readEnvironment, Func<string, string?> readConfiguration)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        ArgumentNullException.ThrowIfNull(readConfiguration);

        var environmentConnection = readEnvironment(RecallRadarDbContextFactory.ConnectionEnvironmentVariable);
        return new AppSettings
        {
            ConnectionString = string.IsNullOrWhiteSpace(environmentConnection)
                ? configuredConnectionString ?? string.Empty
                : environmentConnection,
            AnthropicApiKey = FirstSet(AnthropicKeyVariable, readEnvironment, readConfiguration),
            VoyageApiKey = FirstSet(VoyageKeyVariable, readEnvironment, readConfiguration),
        };
    }

    /// <summary>
    /// Configuration wins where it says anything at all, and the environment fills the gaps.
    /// </summary>
    /// <remarks>
    /// Configuration already includes environment variables, so on a running server the two agree
    /// and this changes nothing. It matters to a test host, which must be able to say "this process
    /// has no key" even on a machine where the vault has exported one into the shell -- otherwise
    /// whether the suite passes depends on what the developer injected an hour ago. An explicitly
    /// configured empty value counts as an answer, which is how absence gets said out loud.
    /// </remarks>
    private static string? FirstSet(string name, Func<string, string?> readEnvironment, Func<string, string?> readConfiguration)
    {
        var fromConfiguration = readConfiguration(name);
        return fromConfiguration is not null ? NullIfBlank(fromConfiguration) : NullIfBlank(readEnvironment(name));
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    /// <summary>Renders capability flags only. Key material and the connection string are never included.</summary>
    public override string ToString() =>
        $"AppSettings(hasAnthropicKey={HasAnthropicKey}, hasVoyageKey={HasVoyageKey}, hasConnectionString={HasConnectionString})";
}
