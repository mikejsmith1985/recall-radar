// Proves the settings object never renders a secret, whatever logs it (Article IX).
using RecallRadar.Api.Config;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Unit.Config;

public sealed class AppSettingsTests
{
    private const string FakeAnthropicKey = "sk-ant-not-a-real-key-0001";
    private const string FakeVoyageKey = "pa-not-a-real-key-0002";
    private const string EnvironmentConnection = "Host=from-env;Password=env-placeholder";
    private const string ConfiguredConnection = "Host=from-config;Password=config-placeholder";

    [Fact]
    public void Load_ReadsKeysFromTheEnvironmentTheVaultInjects()
    {
        var settings = AppSettings.Load(null, ReadFakeEnvironment);

        Assert.True(settings.HasAnthropicKey);
        Assert.True(settings.HasVoyageKey);
        Assert.Equal(FakeAnthropicKey, settings.AnthropicApiKey);
        Assert.Equal(FakeVoyageKey, settings.VoyageApiKey);
    }

    [Fact]
    public void Load_PrefersTheEnvironmentConnectionOverConfiguration()
    {
        var settings = AppSettings.Load(ConfiguredConnection, ReadFakeEnvironment);

        Assert.Equal(EnvironmentConnection, settings.ConnectionString);
    }

    [Fact]
    public void Load_FallsBackToConfigurationWhenTheEnvironmentHasNoConnection()
    {
        var settings = AppSettings.Load(ConfiguredConnection, _ => null);

        Assert.Equal(ConfiguredConnection, settings.ConnectionString);
        Assert.False(settings.HasAnthropicKey);
        Assert.False(settings.HasVoyageKey);
    }

    [Fact]
    public void Load_ReportsMissingVoyageKeyAsCapabilityFlag()
    {
        var settings = AppSettings.Load(null, name => name == AppSettings.AnthropicKeyVariable ? FakeAnthropicKey : null);

        Assert.True(settings.HasAnthropicKey);
        Assert.False(settings.HasVoyageKey);
        Assert.False(settings.HasConnectionString);
    }

    [Fact]
    public void ToString_NeverContainsKeyMaterialOrConnectionString()
    {
        var rendered = AppSettings.Load(ConfiguredConnection, ReadFakeEnvironment).ToString();

        Assert.DoesNotContain(FakeAnthropicKey, rendered);
        Assert.DoesNotContain(FakeVoyageKey, rendered);
        Assert.DoesNotContain("placeholder", rendered);
        Assert.Contains("hasAnthropicKey=True", rendered);
        Assert.Contains("hasConnectionString=True", rendered);
    }

    private static string? ReadFakeEnvironment(string name) => name switch
    {
        AppSettings.AnthropicKeyVariable => FakeAnthropicKey,
        AppSettings.VoyageKeyVariable => FakeVoyageKey,
        RecallRadarDbContextFactory.ConnectionEnvironmentVariable => EnvironmentConnection,
        _ => null,
    };
}
