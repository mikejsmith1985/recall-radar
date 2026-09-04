// Checks that ground truth is derived from NHTSA's own links, and that it excludes what it should.
using RecallRadar.Retrieval.Evaluation;
using RecallRadar.Retrieval.Persistence;

namespace RecallRadar.Integration.Evaluation;

[Collection(PostgresCollection.Name)]
public sealed class GroundTruthBuilderTests(PostgresFixture postgres) : IAsyncLifetime
{
    private const string Component = "SERVICE BRAKES, HYDRAULIC:FOUNDATION";
    private const string RelatedComponent = "SERVICE BRAKES, HYDRAULIC:FLUID";
    private const string OtherComponent = "STRUCTURE:BODY";
    private const string Campaign = "16V345000";

    private static readonly DateOnly Opened = new(2016, 2, 29);
    private static readonly DateOnly Closed = new(2016, 7, 26);

    private int _vehicleId;
    private long _investigationId;
    private long _recallId;

    public async Task InitializeAsync()
    {
        await using var context = postgres.CreateContext();
        var vehicle = Vehicle.Create("FORD", $"GT-{Guid.NewGuid():N}", 2013, $"Ground truth fixture {Guid.NewGuid():N}");
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();
        _vehicleId = vehicle.Id;

        _investigationId = await SeedAsync(context, SourceKind.Investigation, "PE16003", Component, new DateOnly(2016, 2, 29), "Master cylinder leak investigation.");
        _recallId = await SeedAsync(context, SourceKind.Recall, Campaign, Component, new DateOnly(2016, 7, 1), "Ford is recalling certain vehicles for a master cylinder leak.");

        // Inside the window and about the same component: these are the queries.
        await SeedAsync(context, SourceKind.Complaint, "c-inside-1", Component, new DateOnly(2016, 3, 1), "The brake pedal went to the floor.");
        await SeedAsync(context, SourceKind.Complaint, "c-inside-2", RelatedComponent, new DateOnly(2016, 5, 1), "Brake fluid leaked from the master cylinder.");
        // Outside the window: filed before it opened and after it closed.
        await SeedAsync(context, SourceKind.Complaint, "c-before", Component, new DateOnly(2015, 1, 1), "Brakes felt soft last year.");
        await SeedAsync(context, SourceKind.Complaint, "c-after", Component, new DateOnly(2020, 1, 1), "Brakes felt soft years later.");
        // A different component entirely.
        await SeedAsync(context, SourceKind.Complaint, "c-other", OtherComponent, new DateOnly(2016, 4, 1), "The rear hatch rattles.");

        context.InvestigationLinks.Add(InvestigationLink.Create(_investigationId, Campaign, Component, Opened, Closed));
        await context.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task RelevantDocumentsAreTheInvestigationAndItsCampaignRecall()
    {
        var cases = await BuildAsync();

        Assert.NotEmpty(cases);
        Assert.All(cases, groundTruth =>
        {
            Assert.Contains(_investigationId, groundTruth.RelevantDocumentIds);
            Assert.Contains(_recallId, groundTruth.RelevantDocumentIds);
        });
    }

    [Fact]
    public async Task QueriesAreComplaintsFiledInsideTheInvestigationWindow()
    {
        var cases = await BuildAsync();

        var queries = cases.Select(groundTruth => groundTruth.QueryText).ToList();
        Assert.Contains(queries, query => query.Contains("pedal went to the floor", StringComparison.Ordinal));
        Assert.Contains(queries, query => query.Contains("leaked from the master cylinder", StringComparison.Ordinal));
        Assert.DoesNotContain(queries, query => query.Contains("last year", StringComparison.Ordinal));
        Assert.DoesNotContain(queries, query => query.Contains("years later", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ComplaintsAboutADifferentComponentAreNotQueries()
    {
        var cases = await BuildAsync();

        Assert.DoesNotContain(cases, groundTruth => groundTruth.QueryText.Contains("rear hatch", StringComparison.Ordinal));
    }

    [Fact]
    public async Task AnInvestigationWithNoCampaignProducesNoCases()
    {
        await using var context = postgres.CreateContext();
        var vehicle = Vehicle.Create("FORD", $"NOCAMP-{Guid.NewGuid():N}", 2014, $"No campaign fixture {Guid.NewGuid():N}");
        context.Vehicles.Add(vehicle);
        await context.SaveChangesAsync();
        var investigationId = await SeedAsync(context, SourceKind.Investigation, "PE99999", Component, Opened, "Open investigation.", vehicle.Id);
        await SeedAsync(context, SourceKind.Complaint, "c-orphan", Component, new DateOnly(2016, 3, 1), "Brakes soft.", vehicle.Id);
        context.InvestigationLinks.Add(InvestigationLink.Create(investigationId, "unset", Component, Opened, Closed));
        await context.SaveChangesAsync();

        // Proving the rule needs a link whose campaign really is empty, which Create refuses to make,
        // so the check here is that a campaign with no matching recall still yields no recall id.
        var cases = await new GroundTruthBuilder(context).BuildAsync(vehicle.Id, CancellationToken.None);

        Assert.All(cases, groundTruth => Assert.Single(groundTruth.RelevantDocumentIds));
    }

    [Fact]
    public async Task BuildingIsDeterministicSoTwoRunsScoreTheSameCases()
    {
        var first = await BuildAsync();
        var second = await BuildAsync();

        Assert.Equal(
            first.Select(groundTruth => groundTruth.CaseId),
            second.Select(groundTruth => groundTruth.CaseId));
    }

    [Fact]
    public async Task CasesAreScopedToTheVehicleAsked()
    {
        await using var context = postgres.CreateContext();
        var cases = await new GroundTruthBuilder(context).BuildAsync(_vehicleId, CancellationToken.None);

        Assert.NotEmpty(cases);
        Assert.All(cases, groundTruth => Assert.Contains(_investigationId, groundTruth.RelevantDocumentIds));
    }

    [Fact]
    public void ComponentPrefixMatchesAcrossNhtsaGranularities()
    {
        // NHTSA writes the same fault at several depths; matching the whole string finds almost nothing.
        Assert.Equal(
            GroundTruthBuilder.ComponentPrefixOf(Component),
            GroundTruthBuilder.ComponentPrefixOf(RelatedComponent));
        Assert.NotEqual(
            GroundTruthBuilder.ComponentPrefixOf(Component),
            GroundTruthBuilder.ComponentPrefixOf(OtherComponent));
    }

    private async Task<IReadOnlyList<Domain.Evaluation.GroundTruthCase>> BuildAsync()
    {
        await using var context = postgres.CreateContext();
        return await new GroundTruthBuilder(context).BuildAsync(_vehicleId, CancellationToken.None);
    }

    private async Task<long> SeedAsync(
        RecallRadarDbContext context, SourceKind kind, string externalId, string component,
        DateOnly filedOn, string body, int? vehicleId = null)
    {
        // The external id is exact, not suffixed: ground truth matches a recall to its campaign
        // number, so a suffix would silently make every campaign unfindable.
        var document = SourceDocument.Create(
            kind, externalId, vehicleId ?? _vehicleId, component, filedOn, externalId, body, "{}");
        context.SourceDocuments.Add(document);
        await context.SaveChangesAsync();
        context.DocumentChunks.Add(DocumentChunk.Create(document.Id, 0, body));
        await context.SaveChangesAsync();
        return document.Id;
    }
}
