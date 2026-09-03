using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using NpgsqlTypes;
using Pgvector;

#nullable disable

namespace RecallRadar.Retrieval.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:vector", ",,");

            migrationBuilder.CreateTable(
                name: "answers",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    Question = table.Column<string>(type: "text", nullable: false),
                    AnswerJson = table.Column<string>(type: "jsonb", nullable: false),
                    VerifiedCitationCount = table.Column<int>(type: "integer", nullable: false),
                    DroppedCitationCount = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_answers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "vehicles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Make = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NhtsaModel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ModelYear = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vehicles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "source_documents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    VehicleId = table.Column<int>(type: "integer", nullable: false),
                    Component = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FiledOn = table.Column<DateOnly>(type: "date", nullable: true),
                    Title = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    RawPayload = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_source_documents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_source_documents_vehicles_VehicleId",
                        column: x => x.VehicleId,
                        principalTable: "vehicles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_chunks",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DocumentId = table.Column<long>(type: "bigint", nullable: false),
                    Ordinal = table.Column<int>(type: "integer", nullable: false),
                    Text = table.Column<string>(type: "text", nullable: false),
                    Embedding = table.Column<Vector>(type: "vector(1024)", nullable: true),
                    SearchText = table.Column<NpgsqlTsVector>(type: "tsvector", nullable: true)
                        .Annotation("Npgsql:TsVectorConfig", "english")
                        .Annotation("Npgsql:TsVectorProperties", new[] { "Text" })
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_chunks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_chunks_source_documents_DocumentId",
                        column: x => x.DocumentId,
                        principalTable: "source_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "investigation_links",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InvestigationDocumentId = table.Column<long>(type: "bigint", nullable: false),
                    CampaignNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Component = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    OpenedOn = table.Column<DateOnly>(type: "date", nullable: false),
                    ClosedOn = table.Column<DateOnly>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_investigation_links", x => x.Id);
                    table.ForeignKey(
                        name: "FK_investigation_links_source_documents_InvestigationDocumentId",
                        column: x => x.InvestigationDocumentId,
                        principalTable: "source_documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_answers_VehicleId_CreatedAt",
                table: "answers",
                columns: new[] { "VehicleId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_document_chunks_DocumentId_Ordinal",
                table: "document_chunks",
                columns: new[] { "DocumentId", "Ordinal" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_document_chunks_Embedding",
                table: "document_chunks",
                column: "Embedding")
                .Annotation("Npgsql:IndexMethod", "hnsw")
                .Annotation("Npgsql:IndexOperators", new[] { "vector_cosine_ops" });

            migrationBuilder.CreateIndex(
                name: "IX_document_chunks_SearchText",
                table: "document_chunks",
                column: "SearchText")
                .Annotation("Npgsql:IndexMethod", "GIN");

            migrationBuilder.CreateIndex(
                name: "IX_investigation_links_CampaignNumber",
                table: "investigation_links",
                column: "CampaignNumber");

            migrationBuilder.CreateIndex(
                name: "IX_investigation_links_InvestigationDocumentId",
                table: "investigation_links",
                column: "InvestigationDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_source_documents_VehicleId_Component",
                table: "source_documents",
                columns: new[] { "VehicleId", "Component" });

            migrationBuilder.CreateIndex(
                name: "IX_source_documents_VehicleId_Kind_ExternalId",
                table: "source_documents",
                columns: new[] { "VehicleId", "Kind", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_vehicles_Make_NhtsaModel_ModelYear",
                table: "vehicles",
                columns: new[] { "Make", "NhtsaModel", "ModelYear" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "answers");

            migrationBuilder.DropTable(
                name: "document_chunks");

            migrationBuilder.DropTable(
                name: "investigation_links");

            migrationBuilder.DropTable(
                name: "source_documents");

            migrationBuilder.DropTable(
                name: "vehicles");
        }
    }
}
