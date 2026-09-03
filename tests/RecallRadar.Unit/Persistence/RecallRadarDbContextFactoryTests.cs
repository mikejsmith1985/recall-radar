// Checks how the factory resolves its connection string, without opening a connection.
using Microsoft.EntityFrameworkCore;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Unit.Persistence;

public sealed class RecallRadarDbContextFactoryTests
{
    [Fact]
    public void ResolveConnection_ReadsTheEnvironmentVariable()
    {
        const string configured = "Host=db.example;Database=other";

        var resolved = RecallRadarDbContextFactory.ResolveConnection(
            name => name == RecallRadarDbContextFactory.ConnectionEnvironmentVariable ? configured : null);

        Assert.Equal(configured, resolved);
    }

    [Fact]
    public void ResolveConnection_HasNoDefaultBecauseADefaultWouldEmbedAPassword()
    {
        var error = Assert.Throws<InvalidOperationException>(() =>
            RecallRadarDbContextFactory.ResolveConnection(_ => "  "));

        Assert.Contains(RecallRadarDbContextFactory.ConnectionEnvironmentVariable, error.Message);
        Assert.Contains(".env.example", error.Message);
    }

    [Fact]
    public void Configure_RejectsBlankConnectionString()
    {
        var builder = new DbContextOptionsBuilder<RecallRadarDbContext>();

        Assert.Throws<ArgumentException>(() => RecallRadarDbContextFactory.Configure(builder, ""));
    }
}
