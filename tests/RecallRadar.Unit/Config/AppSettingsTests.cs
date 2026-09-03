// Proves the settings object never renders a secret, whatever logs it (Article IX).
using RecallRadar.Api.Config;

namespace RecallRadar.Unit.Config;

public sealed class AppSettingsTests
{
    private const string FakeAnthropicKey = "sk-ant-not-a-real-key-0001";
    private const string FakeVoyageKey = "pa-not-a-real-key-0002";
    private const string Connection = "Host=127.0.0.1;Password=super-secret";

    [Fact]
    public void Load_ReadsKeysFromTheEnvironmentTheVaultInjects()
    {
        var settings = AppSettings.Load(Connection, ReadFakeEnvironment);

        Assert.True(settings.HasAnthropicKey);
        Assert.True(settings.HasVoyageKey);
        Assert.Equal(FakeAnthropicKey, settings.AnthropicApiKey);
        Assert.Equal(FakeVoyageKey, settings.VoyageApiKey);
    }

    [Fact]
    public void Load_ReportsMissingVoyageKeyAsCapabilityFlag()
    {
        var settings = AppSettings.Load(Connection, name => name == AppSettings.AnthropicKeyVariable ? FakeAnthropicKey : null);

        Assert.True(settings.HasAnthropicKey);
        Assert.False(settings.HasVoyageKey);
    }

    [Fact]
    public void ToString_NeverContainsKeyMaterialOrConnectionString()
    {
        var rendered = AppSettings.Load(Connection, ReadFakeEnvironment).ToString();

        Assert.DoesNotContain(FakeAnthropicKey, rendered);
        Assert.DoesNotContain(FakeVoyageKey, rendered);
        Assert.DoesNotContain("super-secret", rendered);
        Assert.Contains("hasAnthropicKey=True", rendered);
    }

    private static string? ReadFakeEnvironment(string name) => name switch
    {
        AppSettings.AnthropicKeyVariable => FakeAnthropicKey,
        AppSettings.VoyageKeyVariable => FakeVoyageKey,
        _ => null,
    };
}
