// Checks how the design-time factory picks its connection string, without opening a connection.
using Microsoft.EntityFrameworkCore;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Unit.Persistence;

public sealed class RecallRadarDbContextFactoryTests
{
    [Fact]
    public void ResolveDesignTimeConnection_PrefersEnvironmentVariable()
    {
        const string configured = "Host=db.example;Database=other";

        var resolved = RecallRadarDbContextFactory.ResolveDesignTimeConnection(
            name => name == RecallRadarDbContextFactory.ConnectionEnvironmentVariable ? configured : null);

        Assert.Equal(configured, resolved);
    }

    [Fact]
    public void ResolveDesignTimeConnection_FallsBackToComposeDatabaseWhenUnset()
    {
        var resolved = RecallRadarDbContextFactory.ResolveDesignTimeConnection(_ => "  ");

        Assert.Equal(RecallRadarDbContextFactory.LocalComposeConnection, resolved);
    }

    [Fact]
    public void Configure_RejectsBlankConnectionString()
    {
        var builder = new DbContextOptionsBuilder<RecallRadarDbContext>();

        Assert.Throws<ArgumentException>(() => RecallRadarDbContextFactory.Configure(builder, ""));
    }
}
