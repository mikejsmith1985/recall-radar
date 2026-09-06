using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RecallRadar.Retrieval.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class IngestJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RecallModel",
                table: "vehicles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ingest_jobs",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VehicleId = table.Column<int>(type: "integer", nullable: true),
                    Make = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    NhtsaModel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    RecallModel = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ModelYear = table.Column<int>(type: "integer", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Trigger = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    QueuedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    ReportJson = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingest_jobs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ingest_jobs_State_QueuedAt",
                table: "ingest_jobs",
                columns: new[] { "State", "QueuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ingest_jobs_VehicleId_QueuedAt",
                table: "ingest_jobs",
                columns: new[] { "VehicleId", "QueuedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ingest_jobs");

            migrationBuilder.DropColumn(
                name: "RecallModel",
                table: "vehicles");
        }
    }
}
