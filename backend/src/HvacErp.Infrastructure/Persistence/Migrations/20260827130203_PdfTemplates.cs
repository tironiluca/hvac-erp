using System;
using System.Linq;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;
using HvacErp.Application.Pdf;
using HvacErp.Domain.Enums;

#nullable disable

namespace HvacErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PdfTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pdf_templates",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    document_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_default = table.Column<bool>(type: "boolean", nullable: false),
                    definition_json = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_pdf_templates", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_pdf_templates_document_type",
                table: "pdf_templates",
                column: "document_type",
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_pdf_templates_document_type_name",
                table: "pdf_templates",
                columns: new[] { "document_type", "name" },
                unique: true);

            // Seed the built-in active template per document type. Its definition is
            // PdfTemplateDefaults (reproduces the pre-Phase-10 hardcoded layout), kept in sync with
            // DevDataSeeder.SeedPdfTemplatesAsync; ON CONFLICT keeps this idempotent.
            foreach (var documentType in Enum.GetValues<PdfDocumentType>())
            {
                var name = PdfTemplateDefaults.DefaultName(documentType).Replace("'", "''");
                var json = PdfJson.Serialize(PdfTemplateDefaults.Default(documentType)).Replace("'", "''");
                migrationBuilder.Sql($@"
                    INSERT INTO pdf_templates
                        (document_type, name, is_active, is_system_default, definition_json, updated_at, updated_by)
                    VALUES
                        ('{documentType}', '{name}', TRUE, TRUE, '{json}'::jsonb, now(), NULL)
                    ON CONFLICT (document_type, name) DO NOTHING;
                ");
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "pdf_templates");
        }
    }
}
