// Proves the migrated schema really has vector search and full-text search wired in.
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using RecallRadar.Integration;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Integration.Persistence;

[Collection(PostgresCollection.Name)]
public sealed class RecallRadarDbContextTests(PostgresFixture postgres)
{
    private const string ComplaintBody = "The contact owns a 2013 Ford Explorer. Exhaust odor enters the passenger cabin while driving.";

    [Fact]
    public async Task Migrations_InstallThePgvectorExtension()
    {
        await using var context = postgres.CreateContext();

        var installed = await context.Database
            .SqlQueryRaw<string>("SELECT extname AS \"Value\" FROM pg_extension WHERE extname = 'vector'")
            .ToListAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["vector"], installed);
    }

    [Fact]
    public async Task Chunk_RoundTripsEmbeddingAndDatabaseGeneratesSearchVector()
    {
        await using var context = postgres.CreateContext();
        var vehicle = Vehicle.Create("FORD", "EXPLORER", 2013, "2013 Explorer Sport (schema test)");
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var document = SourceDocument.Create(
            SourceKind.Complaint, "schema-test-1", vehicle.Id, "STRUCTURE", new DateOnly(2026, 8, 31), "Complaint", ComplaintBody, "{}");
        context.SourceDocuments.Add(document);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        var chunk = DocumentChunk.Create(document.Id, 0, ComplaintBody);
        chunk.SetEmbedding(BuildUnitVector(index: 3));
        context.DocumentChunks.Add(chunk);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        await using var readContext = postgres.CreateContext();
        var stored = await readContext.DocumentChunks.SingleAsync(entity => entity.Id == chunk.Id, TestContext.Current.CancellationToken);
        // A local variable becomes a typed query parameter; an inline call would be inlined as a literal.
        var queryVector = BuildUnitVector(index: 3);
        var nearest = await readContext.DocumentChunks
            .OrderBy(entity => entity.Embedding!.CosineDistance(queryVector))
            .FirstAsync(TestContext.Current.CancellationToken);
        // Scoped to this document: the container is shared by the whole collection, and other tests
        // load real complaints that also mention exhaust odor.
        var fullTextHit = await readContext.DocumentChunks
            .Where(entity => entity.DocumentId == document.Id)
            .Where(entity => entity.SearchText!.Matches(EF.Functions.WebSearchToTsQuery("english", "exhaust odor")))
            .CountAsync(TestContext.Current.CancellationToken);

        Assert.NotNull(stored.SearchText);
        Assert.Equal(chunk.Id, nearest.Id);
        Assert.Equal(1, fullTextHit);
    }

    [Fact]
    public async Task SourceDocument_IsUniquePerVehicleKindAndExternalId()
    {
        await using var context = postgres.CreateContext();
        var vehicle = Vehicle.Create("FORD", "F-150 SUPER CREW", 2014, "2014 F-150 SVT Raptor (schema test)");
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.SourceDocuments.Add(SourceDocument.Create(SourceKind.Recall, "dup-1", vehicle.Id, "SEAT BELTS", null, "Recall", "Body one", "{}"));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        context.SourceDocuments.Add(SourceDocument.Create(SourceKind.Recall, "dup-1", vehicle.Id, "SEAT BELTS", null, "Recall", "Body two", "{}"));

        await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync(TestContext.Current.CancellationToken));
    }

    private static Vector BuildUnitVector(int index)
    {
        var values = new float[DocumentChunk.EmbeddingDimensions];
        values[index] = 1f;
        return new Vector(values);
    }
}
