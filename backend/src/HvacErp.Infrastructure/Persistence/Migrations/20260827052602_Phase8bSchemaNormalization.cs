using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HvacErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase8bSchemaNormalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "confirmed",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "confirmed_date",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "drain_pump",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "electrical_wiring",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "extension_ladder",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "extra_holes_count",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "extra_trace_meters",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "insulation_fixing",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "pipe_extension_meters",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "pipe_inversion",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "recall_notes",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "recalled",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "recalled_date",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "roof_bracket",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "scaffolding",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "survey_date",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "survey_done",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "wifi",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "discount1",
                table: "product_price_lists");

            migrationBuilder.DropColumn(
                name: "discount2",
                table: "product_price_lists");

            migrationBuilder.DropColumn(
                name: "discount3",
                table: "product_price_lists");

            migrationBuilder.DropColumn(
                name: "discount4",
                table: "product_price_lists");

            migrationBuilder.DropColumn(
                name: "discount5",
                table: "product_price_lists");

            migrationBuilder.DropColumn(
                name: "discount6",
                table: "product_price_lists");

            migrationBuilder.DropColumn(
                name: "duct_count",
                table: "ducted_units");

            migrationBuilder.DropColumn(
                name: "outlet_code1",
                table: "ducted_units");

            migrationBuilder.DropColumn(
                name: "outlet_code2",
                table: "ducted_units");

            migrationBuilder.DropColumn(
                name: "outlet_code3",
                table: "ducted_units");

            migrationBuilder.DropColumn(
                name: "outlet_code4",
                table: "ducted_units");

            migrationBuilder.DropColumn(
                name: "outlet_code5",
                table: "ducted_units");

            migrationBuilder.DropColumn(
                name: "outlet_quantity1",
                table: "ducted_units");

            migrationBuilder.DropColumn(
                name: "outlet_quantity2",
                table: "ducted_units");

            migrationBuilder.DropColumn(
                name: "outlet_quantity3",
                table: "ducted_units");

            migrationBuilder.DropColumn(
                name: "outlet_quantity4",
                table: "ducted_units");

            migrationBuilder.DropColumn(
                name: "outlet_quantity5",
                table: "ducted_units");

            migrationBuilder.DropColumn(
                name: "system_type",
                table: "ducted_units");

            // Existing rows predate the status-event log; land them at the initial lifecycle state.
            // (The three legacy bool flags they carried are dropped above; historical survey/recall/
            // confirm timing is not backfilled — acceptable, the DB holds only dev/test quotes.)
            migrationBuilder.AddColumn<string>(
                name: "current_status",
                table: "quotes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Draft");

            migrationBuilder.CreateTable(
                name: "ducted_unit_outlets",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ducted_unit_id = table.Column<int>(type: "integer", nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    system_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    vent_count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ducted_unit_outlets", x => x.id);
                    table.ForeignKey(
                        name: "fk_ducted_unit_outlets_ducted_units_ducted_unit_id",
                        column: x => x.ducted_unit_id,
                        principalTable: "ducted_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "extra_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    label = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    default_price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    quantity_based = table.Column<bool>(type: "boolean", nullable: false),
                    excluded_from_vat = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_extra_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "price_list_discount_tiers",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    price_list_id = table.Column<int>(type: "integer", nullable: false),
                    tier_order = table.Column<int>(type: "integer", nullable: false),
                    percentage = table.Column<decimal>(type: "numeric(12,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_price_list_discount_tiers", x => x.id);
                    table.ForeignKey(
                        name: "fk_price_list_discount_tiers_product_price_lists_price_list_id",
                        column: x => x.price_list_id,
                        principalTable: "product_price_lists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quote_status_events",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    quote_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    changed_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quote_status_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_quote_status_events_quotes_quote_id",
                        column: x => x.quote_id,
                        principalTable: "quotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quote_extras",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    quote_id = table.Column<int>(type: "integer", nullable: false),
                    extra_type_id = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quote_extras", x => x.id);
                    table.ForeignKey(
                        name: "fk_quote_extras_extra_types_extra_type_id",
                        column: x => x.extra_type_id,
                        principalTable: "extra_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_quote_extras_quotes_quote_id",
                        column: x => x.quote_id,
                        principalTable: "quotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ducted_unit_outlets_ducted_unit_id",
                table: "ducted_unit_outlets",
                column: "ducted_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_extra_types_code",
                table: "extra_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_price_list_discount_tiers_price_list_id_tier_order",
                table: "price_list_discount_tiers",
                columns: new[] { "price_list_id", "tier_order" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quote_extras_extra_type_id",
                table: "quote_extras",
                column: "extra_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_quote_extras_quote_id",
                table: "quote_extras",
                column: "quote_id");

            migrationBuilder.CreateIndex(
                name: "ix_quote_status_events_quote_id_occurred_at",
                table: "quote_status_events",
                columns: new[] { "quote_id", "occurred_at" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ducted_unit_outlets");

            migrationBuilder.DropTable(
                name: "price_list_discount_tiers");

            migrationBuilder.DropTable(
                name: "quote_extras");

            migrationBuilder.DropTable(
                name: "quote_status_events");

            migrationBuilder.DropTable(
                name: "extra_types");

            migrationBuilder.DropColumn(
                name: "current_status",
                table: "quotes");

            migrationBuilder.AddColumn<bool>(
                name: "confirmed",
                table: "quotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "confirmed_date",
                table: "quotes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "drain_pump",
                table: "quotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "electrical_wiring",
                table: "quotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "extension_ladder",
                table: "quotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "extra_holes_count",
                table: "quotes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "extra_trace_meters",
                table: "quotes",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "insulation_fixing",
                table: "quotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "pipe_extension_meters",
                table: "quotes",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "pipe_inversion",
                table: "quotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "recall_notes",
                table: "quotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "recalled",
                table: "quotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "recalled_date",
                table: "quotes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "roof_bracket",
                table: "quotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "scaffolding",
                table: "quotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "survey_date",
                table: "quotes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "survey_done",
                table: "quotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "wifi",
                table: "quotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "discount1",
                table: "product_price_lists",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "discount2",
                table: "product_price_lists",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "discount3",
                table: "product_price_lists",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "discount4",
                table: "product_price_lists",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "discount5",
                table: "product_price_lists",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "discount6",
                table: "product_price_lists",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "duct_count",
                table: "ducted_units",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "outlet_code1",
                table: "ducted_units",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "outlet_code2",
                table: "ducted_units",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "outlet_code3",
                table: "ducted_units",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "outlet_code4",
                table: "ducted_units",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "outlet_code5",
                table: "ducted_units",
                type: "character varying(1)",
                maxLength: 1,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "outlet_quantity1",
                table: "ducted_units",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "outlet_quantity2",
                table: "ducted_units",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "outlet_quantity3",
                table: "ducted_units",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "outlet_quantity4",
                table: "ducted_units",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "outlet_quantity5",
                table: "ducted_units",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "system_type",
                table: "ducted_units",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }
    }
}
