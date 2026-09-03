// Checks that the NHTSA clients are registered, without building a provider or touching the network.
using Microsoft.Extensions.DependencyInjection;
using RecallRadar.Ingest.Nhtsa;

namespace RecallRadar.Unit.Nhtsa;

public sealed class NhtsaServiceCollectionExtensionsTests
{
    [Fact]
    public void AddNhtsaClients_RegistersEveryTypedClient()
    {
        var services = new ServiceCollection();

        services.AddNhtsaClients();

        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(NhtsaComplaintsClient));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(NhtsaRecallsClient));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(NhtsaModelsClient));
        Assert.Contains(services, descriptor => descriptor.ServiceType == typeof(NhtsaFlatFileClient));
    }

    [Fact]
    public void Timeouts_LeaveRoomForNhtsaToBeSlow()
    {
        Assert.True(NhtsaServiceCollectionExtensions.TotalTimeout > NhtsaServiceCollectionExtensions.AttemptTimeout);
        Assert.True(NhtsaServiceCollectionExtensions.AttemptTimeout >= TimeSpan.FromSeconds(30));
    }
}
