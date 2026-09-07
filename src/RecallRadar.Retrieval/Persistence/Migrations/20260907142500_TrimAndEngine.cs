using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace RecallRadar.Retrieval.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TrimAndEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "EngineCylinders",
                table: "vehicles",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EngineLitres",
                table: "vehicles",
                type: "numeric(4,1)",
                precision: 4,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Trim",
                table: "vehicles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrimKey",
                table: "vehicles",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Vin",
                table: "vehicles",
                type: "character varying(17)",
                maxLength: 17,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EngineCylinders",
                table: "source_documents",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EngineLitres",
                table: "source_documents",
                type: "numeric(4,1)",
                precision: 4,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Trim",
                table: "source_documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrimKey",
                table: "source_documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VinDescriptor",
                table: "source_documents",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "vin_decodes",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Descriptor = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    ModelYear = table.Column<int>(type: "integer", nullable: false),
                    Trim = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    TrimKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    EngineLitres = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    EngineCylinders = table.Column<int>(type: "integer", nullable: true),
                    DecodedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_vin_decodes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_source_documents_VehicleId_TrimKey_EngineLitres_EngineCylin~",
                table: "source_documents",
                columns: new[] { "VehicleId", "TrimKey", "EngineLitres", "EngineCylinders" });

            migrationBuilder.CreateIndex(
                name: "IX_vin_decodes_Descriptor_ModelYear",
                table: "vin_decodes",
                columns: new[] { "Descriptor", "ModelYear" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "vin_decodes");

            migrationBuilder.DropIndex(
                name: "IX_source_documents_VehicleId_TrimKey_EngineLitres_EngineCylin~",
                table: "source_documents");

            migrationBuilder.DropColumn(
                name: "EngineCylinders",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "EngineLitres",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "Trim",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "TrimKey",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "Vin",
                table: "vehicles");

            migrationBuilder.DropColumn(
                name: "EngineCylinders",
                table: "source_documents");

            migrationBuilder.DropColumn(
                name: "EngineLitres",
                table: "source_documents");

            migrationBuilder.DropColumn(
                name: "Trim",
                table: "source_documents");

            migrationBuilder.DropColumn(
                name: "TrimKey",
                table: "source_documents");

            migrationBuilder.DropColumn(
                name: "VinDescriptor",
                table: "source_documents");
        }
    }
}
