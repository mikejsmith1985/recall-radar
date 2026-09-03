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
        var environmentConnection = readEnvironment(RecallRadarDbContextFactory.ConnectionEnvironmentVariable);
        return new AppSettings
        {
            ConnectionString = string.IsNullOrWhiteSpace(environmentConnection)
                ? configuredConnectionString ?? string.Empty
                : environmentConnection,
            AnthropicApiKey = readEnvironment(AnthropicKeyVariable),
            VoyageApiKey = readEnvironment(VoyageKeyVariable),
        };
    }

    /// <summary>Renders capability flags only. Key material and the connection string are never included.</summary>
    public override string ToString() =>
        $"AppSettings(hasAnthropicKey={HasAnthropicKey}, hasVoyageKey={HasVoyageKey}, hasConnectionString={HasConnectionString})";
}
