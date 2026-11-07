using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HvacErp.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// English product defaults without disturbing existing (Italian) deployments: values the code
    /// matches on become stable codes, and existing data is backfilled so it keeps working with its
    /// own labels.
    /// <list type="bullet">
    /// <item><c>service_items.code</c>: the pricing engine's rows, matched by their legacy names.</item>
    /// <item><c>products.is_hydronic</c>: replaces the "Idronico" brand check.</item>
    /// <item><c>products.category</c>: <c>Esterna</c>/<c>Interna</c> become <c>Outdoor</c>/<c>Indoor</c>.</item>
    /// <item>PDF templates saved before the language setting existed were Italian: they get <c>"language": "it"</c>.</item>
    /// </list>
    /// On a fresh database every statement matches nothing; the English seeds come from DevDataSeeder.
    /// </summary>
    public partial class EnglishDefaultsAndStableCodes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "code",
                table: "service_items",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_hydronic",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "ix_service_items_code",
                table: "service_items",
                column: "code",
                unique: true);

            migrationBuilder.Sql("""
                UPDATE service_items SET code = CASE name
                    WHEN 'Mono' THEN 'BASE_SYSTEM_1'
                    WHEN 'Dual' THEN 'BASE_SYSTEM_2'
                    WHEN 'Trial' THEN 'BASE_SYSTEM_3'
                    WHEN 'Poker' THEN 'BASE_SYSTEM_4'
                    WHEN 'Penta' THEN 'BASE_SYSTEM_5'
                    WHEN 'Plenum canalizzato' THEN 'DUCTED_PLENUM'
                    WHEN 'Modulo Cloud canalizzato' THEN 'DUCTED_CLOUD_MODULE'
                    WHEN 'Tubo flessibile canalizzato (al metro)' THEN 'DUCTED_FLEXIBLE_HOSE_M'
                    WHEN 'Tubo rigido canalizzato (al metro)' THEN 'DUCTED_RIGID_HOSE_M'
                    WHEN 'Telecomando canalizzato' THEN 'DUCTED_REMOTE'
                END
                WHERE id IN (
                    -- Lowest id per legacy name, so a duplicated name cannot break the unique index.
                    SELECT min(id) FROM service_items
                    WHERE name IN ('Mono', 'Dual', 'Trial', 'Poker', 'Penta', 'Plenum canalizzato',
                        'Modulo Cloud canalizzato', 'Tubo flessibile canalizzato (al metro)',
                        'Tubo rigido canalizzato (al metro)', 'Telecomando canalizzato')
                    GROUP BY name);

                UPDATE products SET is_hydronic = true WHERE lower(brand) = 'idronico';

                UPDATE products SET category = 'Outdoor' WHERE category = 'Esterna';
                UPDATE products SET category = 'Indoor' WHERE category = 'Interna';

                UPDATE pdf_templates
                SET definition_json = jsonb_set(definition_json, '{page,language}', '"it"')
                WHERE definition_json ? 'page' AND definition_json #>> '{page,language}' IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE products SET category = 'Esterna' WHERE category = 'Outdoor';
                UPDATE products SET category = 'Interna' WHERE category = 'Indoor';
                UPDATE pdf_templates SET definition_json = definition_json #- '{page,language}';
                """);

            migrationBuilder.DropIndex(
                name: "ix_service_items_code",
                table: "service_items");

            migrationBuilder.DropColumn(
                name: "code",
                table: "service_items");

            migrationBuilder.DropColumn(
                name: "is_hydronic",
                table: "products");
        }
    }
}
