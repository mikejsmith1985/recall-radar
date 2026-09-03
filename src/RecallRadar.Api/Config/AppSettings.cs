// Strongly-typed application settings that refuse to render secrets (Article IX).
namespace RecallRadar.Api.Config;

/// <summary>
/// Everything the API needs from configuration. Secrets are held but never appear in
/// <see cref="ToString"/>, so an accidental log of the settings object leaks nothing.
/// </summary>
public sealed class AppSettings
{
    public const string SectionName = "RecallRadar";
    public const string AnthropicKeyVariable = "ANTHROPIC_API_KEY";
    public const string VoyageKeyVariable = "VOYAGE_API_KEY";

    public string ConnectionString { get; init; } = string.Empty;
    public string? AnthropicApiKey { get; init; }
    public string? VoyageApiKey { get; init; }

    public bool HasAnthropicKey => !string.IsNullOrWhiteSpace(AnthropicApiKey);
    public bool HasVoyageKey => !string.IsNullOrWhiteSpace(VoyageApiKey);

    /// <summary>Reads settings from configuration sections plus the environment variables the vault injects.</summary>
    public static AppSettings Load(string? connectionString, Func<string, string?> readEnvironment)
    {
        ArgumentNullException.ThrowIfNull(readEnvironment);
        return new AppSettings
        {
            ConnectionString = connectionString ?? string.Empty,
            AnthropicApiKey = readEnvironment(AnthropicKeyVariable),
            VoyageApiKey = readEnvironment(VoyageKeyVariable),
        };
    }

    /// <summary>Renders capability flags only. Key material and the connection string are never included.</summary>
    public override string ToString() =>
        $"AppSettings(hasAnthropicKey={HasAnthropicKey}, hasVoyageKey={HasVoyageKey}, hasConnectionString={!string.IsNullOrWhiteSpace(ConnectionString)})";
}
