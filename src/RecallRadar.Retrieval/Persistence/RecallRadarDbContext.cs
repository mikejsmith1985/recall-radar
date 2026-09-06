// The EF Core model for Recall Radar: NHTSA records, their retrievable chunks, and answer audit rows.
using Microsoft.EntityFrameworkCore;

namespace RecallRadar.Retrieval.Persistence;

/// <summary>
/// Maps the entities to PostgreSQL. Vector search (pgvector, HNSW) and full-text search
/// (tsvector, GIN) both live in this one database so hybrid retrieval is a single query.
/// </summary>
public sealed class RecallRadarDbContext(DbContextOptions<RecallRadarDbContext> options) : DbContext(options)
{
    private const string FullTextLanguage = "english";
    private const string HnswIndexMethod = "hnsw";
    private const string CosineOperatorClass = "vector_cosine_ops";
    private const string GinIndexMethod = "GIN";

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<SourceDocument> SourceDocuments => Set<SourceDocument>();
    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();
    public DbSet<InvestigationLink> InvestigationLinks => Set<InvestigationLink>();
    public DbSet<Answer> Answers => Set<Answer>();
    public DbSet<EvaluationRun> EvaluationRuns => Set<EvaluationRun>();
    public DbSet<IngestJob> IngestJobs => Set<IngestJob>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        ConfigureVehicle(modelBuilder);
        ConfigureSourceDocument(modelBuilder);
        ConfigureDocumentChunk(modelBuilder);
        ConfigureInvestigationLink(modelBuilder);
        ConfigureAnswer(modelBuilder);
        ConfigureEvaluationRun(modelBuilder);
        ConfigureIngestJob(modelBuilder);
    }

    private static void ConfigureVehicle(ModelBuilder modelBuilder)
    {
        var vehicle = modelBuilder.Entity<Vehicle>();
        vehicle.ToTable("vehicles");
        vehicle.Property(entity => entity.Make).HasMaxLength(64);
        vehicle.Property(entity => entity.NhtsaModel).HasMaxLength(64);
        vehicle.Property(entity => entity.DisplayName).HasMaxLength(128);
        vehicle.Property(entity => entity.RecallModel).HasMaxLength(64);
        // One row per NHTSA identity, so re-running ingestion for the same truck is idempotent.
        vehicle.HasIndex(entity => new { entity.Make, entity.NhtsaModel, entity.ModelYear }).IsUnique();
    }

    private static void ConfigureIngestJob(ModelBuilder modelBuilder)
    {
        var job = modelBuilder.Entity<IngestJob>();
        job.ToTable("ingest_jobs");
        job.Property(entity => entity.Make).HasMaxLength(64);
        job.Property(entity => entity.NhtsaModel).HasMaxLength(64);
        job.Property(entity => entity.RecallModel).HasMaxLength(64);
        job.Property(entity => entity.DisplayName).HasMaxLength(128);
        job.Property(entity => entity.Message).HasMaxLength(IngestJob.MaximumMessageLength);
        // The runner asks for the oldest waiting job, and the API asks for a vehicle's latest one.
        job.HasIndex(entity => new { entity.State, entity.QueuedAt });
        job.HasIndex(entity => new { entity.VehicleId, entity.QueuedAt });
    }

    private static void ConfigureSourceDocument(ModelBuilder modelBuilder)
    {
        var document = modelBuilder.Entity<SourceDocument>();
        document.ToTable("source_documents");
        document.Property(entity => entity.ExternalId).HasMaxLength(64);
        document.Property(entity => entity.Component).HasMaxLength(256);
        document.Property(entity => entity.Title).HasMaxLength(512);
        document.HasOne(entity => entity.Vehicle).WithMany().HasForeignKey(entity => entity.VehicleId);
        // The same recall campaign applies to both trucks, so uniqueness is per vehicle.
        document.HasIndex(entity => new { entity.VehicleId, entity.Kind, entity.ExternalId }).IsUnique();
        document.HasIndex(entity => new { entity.VehicleId, entity.Component });
    }

    private static void ConfigureDocumentChunk(ModelBuilder modelBuilder)
    {
        var chunk = modelBuilder.Entity<DocumentChunk>();
        chunk.ToTable("document_chunks");
        chunk.HasOne(entity => entity.Document).WithMany(entity => entity.Chunks).HasForeignKey(entity => entity.DocumentId);
        chunk.HasIndex(entity => new { entity.DocumentId, entity.Ordinal }).IsUnique();
        chunk.Property(entity => entity.Embedding).HasColumnType($"vector({DocumentChunk.EmbeddingDimensions})");
        chunk.HasIndex(entity => entity.Embedding).HasMethod(HnswIndexMethod).HasOperators(CosineOperatorClass);
        // The database maintains the tsvector itself, so sparse retrieval can never drift from the text.
        chunk.HasGeneratedTsVectorColumn(entity => entity.SearchText!, FullTextLanguage, entity => new { entity.Text });
        chunk.HasIndex(entity => entity.SearchText!).HasMethod(GinIndexMethod);
    }

    private static void ConfigureInvestigationLink(ModelBuilder modelBuilder)
    {
        var link = modelBuilder.Entity<InvestigationLink>();
        link.ToTable("investigation_links");
        link.Property(entity => entity.CampaignNumber).HasMaxLength(32);
        link.Property(entity => entity.Component).HasMaxLength(256);
        link.HasOne(entity => entity.InvestigationDocument).WithMany().HasForeignKey(entity => entity.InvestigationDocumentId);
        link.HasIndex(entity => entity.CampaignNumber);
    }

    private static void ConfigureEvaluationRun(ModelBuilder modelBuilder)
    {
        var run = modelBuilder.Entity<EvaluationRun>();
        run.ToTable("evaluation_runs");
        run.Property(entity => entity.MetricsJson).HasColumnType("jsonb");
        // Newest first is how every reader wants this: the latest run, then the trend behind it.
        run.HasIndex(entity => entity.RanAt).IsDescending();
    }

    private static void ConfigureAnswer(ModelBuilder modelBuilder)
    {
        var answer = modelBuilder.Entity<Answer>();
        answer.ToTable("answers");
        answer.Property(entity => entity.AnswerJson).HasColumnType("jsonb");
        answer.HasIndex(entity => new { entity.VehicleId, entity.CreatedAt });
    }
}
