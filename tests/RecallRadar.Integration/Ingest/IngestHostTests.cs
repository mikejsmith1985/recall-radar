// Checks the composition root: connection resolution and that the host can be built and resolve the service.
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RecallRadar.Ingest;
using RecallRadar.Ingest.Config;
using RecallRadar.Ingest.Nhtsa;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Integration.Ingest;

public sealed class IngestHostTests
{
    private const string ConfiguredConnection = "Host=from-config;Database=placeholder";
    private const string EnvironmentConnection = "Host=from-env;Database=placeholder";

    [Fact]
    public void ResolveConnection_PrefersTheConfigurationOverrideThenTheEnvironmentKey()
    {
        var both = BuildConfiguration(new() { [IngestHost.ConnectionConfigurationKey] = ConfiguredConnection, [RecallRadarDbContextFactory.ConnectionEnvironmentVariable] = EnvironmentConnection });
        var environmentOnly = BuildConfiguration(new() { [RecallRadarDbContextFactory.ConnectionEnvironmentVariable] = EnvironmentConnection });

        Assert.Equal(ConfiguredConnection, IngestHost.ResolveConnection(both));
        Assert.Equal(EnvironmentConnection, IngestHost.ResolveConnection(environmentOnly));
    }

    [Fact]
    public void ResolveConnection_HasNoDefaultBecauseADefaultWouldEmbedAPassword()
    {
        var error = Assert.Throws<InvalidOperationException>(() => IngestHost.ResolveConnection(BuildConfiguration([])));

        Assert.Contains(RecallRadarDbContextFactory.ConnectionEnvironmentVariable, error.Message);
    }

    [Fact]
    public void CreateBuilder_ReadsItsOwnSettingsFileFromAnyWorkingDirectory()
    {
        // A command-line tool is run from wherever the operator is standing, so its registered
        // vehicles must not depend on the shell's current directory.
        var originalDirectory = Directory.GetCurrentDirectory();
        try
        {
            Directory.SetCurrentDirectory(Path.GetTempPath());
            var builder = IngestHost.CreateBuilder([]);
            var options = new IngestOptions();
            builder.Configuration.GetSection(IngestOptions.SectionName).Bind(options);

            Assert.NotEmpty(options.Vehicles);
            Assert.NotNull(options.FindByDisplayName("2013 Explorer Sport"));
            Assert.NotNull(options.FindByDisplayName("2014 F-150 SVT Raptor"));
        }
        finally
        {
            Directory.SetCurrentDirectory(originalDirectory);
        }
    }

    [Fact]
    public void CreateBuilder_WiresTheServiceAndEveryClient()
    {
        var builder = IngestHost.CreateBuilder([]);
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { [IngestHost.ConnectionConfigurationKey] = ConfiguredConnection });
        using var host = builder.Build();
        using var scope = host.Services.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IngestService>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<NhtsaFlatFileClient>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<RecallRadarDbContext>());
    }

    private static IConfiguration BuildConfiguration(Dictionary<string, string?> values) =>
        new ConfigurationBuilder().AddInMemoryCollection(values).Build();
}
