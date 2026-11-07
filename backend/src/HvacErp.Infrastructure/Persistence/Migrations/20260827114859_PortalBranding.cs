using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HvacErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PortalBranding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "portal_brandings",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false),
                    primary_seed = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    tertiary_seed = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    neutral_seed = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: false),
                    portal_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    logo_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    logo_dark_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    favicon_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    login_background_url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_portal_brandings", x => x.id);
                });

            // Seed the single row to the product defaults so a fresh
            // database themes identically to the SCSS fallback. Kept in sync with
            // DevDataSeeder.SeedPortalBrandingAsync; ON CONFLICT keeps this idempotent.
            migrationBuilder.Sql(@"
                INSERT INTO portal_brandings
                    (id, primary_seed, tertiary_seed, neutral_seed, portal_title,
                     logo_url, logo_dark_url, favicon_url, login_background_url, updated_at, updated_by)
                VALUES
                    (1, '#2B6CB0', '#DD6B20', '#5F6368', 'HVAC ERP',
                     NULL, NULL, NULL, NULL, now(), NULL)
                ON CONFLICT (id) DO NOTHING;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "portal_brandings");
        }
    }
}
