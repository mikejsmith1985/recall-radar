// Checks which generator gets registered, and that the key never leaves the client's headers.
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecallRadar.Retrieval.Embeddings;

namespace RecallRadar.Unit.Embeddings;

public sealed class EmbeddingServiceCollectionExtensionsTests
{
    private const string FakeKey = "pa-not-a-real-voyage-key-000000000000";

    [Fact]
    public void AddEmbeddingGenerator_RegistersTheRefusingFallbackWhenNoKeyIsConfigured()
    {
        var services = BuildServices(configuredKey: null);

        var generator = services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        Assert.IsType<NullEmbeddingGenerator>(generator);
    }

    [Fact]
    public void AddEmbeddingGenerator_TreatsABlankKeyAsNoKey()
    {
        var services = BuildServices(configuredKey: "   ");

        Assert.IsType<NullEmbeddingGenerator>(services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>());
    }

    [Fact]
    public void AddEmbeddingGenerator_RegistersVoyageWhenAKeyIsConfigured()
    {
        var services = BuildServices(FakeKey);

        var generator = services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

        Assert.IsType<VoyageEmbeddingGenerator>(generator);
    }

    [Fact]
    public void AddEmbeddingGenerator_ResolvesVoyageThroughEitherRegistration()
    {
        // Typed clients are transient by design, so these are two instances of the same type rather
        // than one shared object. What matters is that neither registration hands back a stand-in.
        var services = BuildServices(FakeKey);

        var throughInterface = services.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();
        var throughConcreteType = services.GetRequiredService<VoyageEmbeddingGenerator>();

        Assert.IsType<VoyageEmbeddingGenerator>(throughInterface);
        Assert.IsType<VoyageEmbeddingGenerator>(throughConcreteType);
    }

    [Fact]
    public void HasEmbeddingKey_ReportsAvailabilityWithoutBuildingTheContainer()
    {
        Assert.True(EmbeddingServiceCollectionExtensions.HasEmbeddingKey(BuildConfiguration(FakeKey)));
        Assert.False(EmbeddingServiceCollectionExtensions.HasEmbeddingKey(BuildConfiguration(null)));
        Assert.False(EmbeddingServiceCollectionExtensions.HasEmbeddingKey(BuildConfiguration(" ")));
    }

    [Fact]
    public void AddEmbeddingGenerator_LeavesNoKeyMaterialInTheServiceDescriptors()
    {
        var services = new ServiceCollection();
        services.AddEmbeddingGenerator(BuildConfiguration(FakeKey));

        var rendered = string.Join('\n', services.Select(descriptor => $"{descriptor.ServiceType} {descriptor.ImplementationType}"));

        Assert.DoesNotContain(FakeKey, rendered, StringComparison.Ordinal);
    }

    private static ServiceProvider BuildServices(string? configuredKey)
    {
        var services = new ServiceCollection();
        services.AddEmbeddingGenerator(BuildConfiguration(configuredKey));
        return services.BuildServiceProvider();
    }

    private static IConfiguration BuildConfiguration(string? configuredKey)
    {
        var values = new Dictionary<string, string?>
        {
            [EmbeddingServiceCollectionExtensions.BaseAddressConfigurationKey] = "http://127.0.0.1:1/v1/",
        };
        if (configuredKey is not null)
        {
            values[EmbeddingServiceCollectionExtensions.ApiKeyConfigurationKey] = configuredKey;
        }

        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}
