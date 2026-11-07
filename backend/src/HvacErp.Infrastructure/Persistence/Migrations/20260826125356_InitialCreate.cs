using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HvacErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "client_sources",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    description = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_client_sources", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "installers",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    mobile = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    department = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    crew = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_installers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "operators",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    extension = table.Column<int>(type: "integer", nullable: true),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_operators", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "product_price_lists",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    vat = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    markup = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    initial_discount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    discount1 = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    discount2 = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    discount3 = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    discount4 = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    discount5 = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    discount6 = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    transport_fee = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    waste_disposal_fee = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    deposit_fee = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    covers_external_code = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_price_lists", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "product_types",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_types", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "clients",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    full_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    address = table.Column<string>(type: "text", nullable: true),
                    city = table.Column<string>(type: "text", nullable: true),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    mobile = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    tax_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    shipping_address = table.Column<string>(type: "text", nullable: true),
                    shipping_city = table.Column<string>(type: "text", nullable: true),
                    source_id = table.Column<int>(type: "integer", nullable: true),
                    referred_by_client_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_clients", x => x.id);
                    table.ForeignKey(
                        name: "fk_clients_client_sources_source_id",
                        column: x => x.source_id,
                        principalTable: "client_sources",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_clients_clients_referred_by_client_id",
                        column: x => x.referred_by_client_id,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    installer_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_users", x => x.id);
                    table.ForeignKey(
                        name: "fk_users_installers_installer_id",
                        column: x => x.installer_id,
                        principalTable: "installers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    price_list_id = table.Column<int>(type: "integer", nullable: false),
                    brand = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    category = table.Column<string>(type: "text", nullable: true),
                    accessories = table.Column<string>(type: "text", nullable: true),
                    cooling_class = table.Column<string>(type: "text", nullable: true),
                    heating_class = table.Column<string>(type: "text", nullable: true),
                    cover_code = table.Column<string>(type: "text", nullable: true),
                    color = table.Column<string>(type: "text", nullable: true),
                    dimensions = table.Column<string>(type: "text", nullable: true),
                    ean = table.Column<string>(type: "character varying(13)", maxLength: 13, nullable: true),
                    extra_price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    filters = table.Column<string>(type: "text", nullable: true),
                    ref_warranty = table.Column<string>(type: "text", nullable: true),
                    stock = table.Column<int>(type: "integer", nullable: false),
                    stock_notes = table.Column<string>(type: "text", nullable: true),
                    photo_id = table.Column<string>(type: "text", nullable: true),
                    location = table.Column<string>(type: "text", nullable: true),
                    max_btu = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    max_liters = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    power = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    hydronic_price = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    price_not_prepared = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    price_prepared = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    noise_level = table.Column<string>(type: "text", nullable: true),
                    refrigerant_gas = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    wifi = table.Column<bool>(type: "boolean", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_products", x => x.id);
                    table.ForeignKey(
                        name: "fk_products_product_price_lists_price_list_id",
                        column: x => x.price_list_id,
                        principalTable: "product_price_lists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "service_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    product_type_id = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_service_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_service_items_product_types_product_type_id",
                        column: x => x.product_type_id,
                        principalTable: "product_types",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "quotes",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    number = table.Column<int>(type: "integer", nullable: false),
                    version = table.Column<int>(type: "integer", nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    parent_quote_id = table.Column<int>(type: "integer", nullable: true),
                    client_id = table.Column<int>(type: "integer", nullable: false),
                    operator_id = table.Column<int>(type: "integer", nullable: true),
                    department = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    survey_done = table.Column<bool>(type: "boolean", nullable: false),
                    survey_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    recalled = table.Column<bool>(type: "boolean", nullable: false),
                    recalled_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    recall_notes = table.Column<string>(type: "text", nullable: true),
                    confirmed = table.Column<bool>(type: "boolean", nullable: false),
                    confirmed_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    predisposed = table.Column<bool>(type: "boolean", nullable: false),
                    electrical_wiring = table.Column<bool>(type: "boolean", nullable: false),
                    roof_bracket = table.Column<bool>(type: "boolean", nullable: false),
                    scaffolding = table.Column<bool>(type: "boolean", nullable: false),
                    extension_ladder = table.Column<bool>(type: "boolean", nullable: false),
                    drain_pump = table.Column<bool>(type: "boolean", nullable: false),
                    insulation_fixing = table.Column<bool>(type: "boolean", nullable: false),
                    pipe_extension_meters = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    extra_holes_count = table.Column<int>(type: "integer", nullable: false),
                    pipe_inversion = table.Column<bool>(type: "boolean", nullable: false),
                    extra_trace_meters = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    wifi = table.Column<bool>(type: "boolean", nullable: false),
                    vat = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    manual_discount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    machines_price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    installation_price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    rounding = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    final_price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    enea_eligible = table.Column<bool>(type: "boolean", nullable: false),
                    enea_price = table.Column<decimal>(type: "numeric(12,2)", nullable: true),
                    installation_position_notes = table.Column<string>(type: "text", nullable: true),
                    floor_plan_image_url = table.Column<string>(type: "text", nullable: true),
                    pdf_url = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quotes", x => x.id);
                    table.ForeignKey(
                        name: "fk_quotes_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_quotes_operators_operator_id",
                        column: x => x.operator_id,
                        principalTable: "operators",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_quotes_quotes_parent_quote_id",
                        column: x => x.parent_quote_id,
                        principalTable: "quotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ducted_units",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    quote_id = table.Column<int>(type: "integer", nullable: false),
                    system_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    duct_count = table.Column<int>(type: "integer", nullable: false),
                    outlet_code1 = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    outlet_code2 = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    outlet_code3 = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    outlet_code4 = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    outlet_code5 = table.Column<string>(type: "character varying(1)", maxLength: 1, nullable: true),
                    outlet_quantity1 = table.Column<int>(type: "integer", nullable: false),
                    outlet_quantity2 = table.Column<int>(type: "integer", nullable: false),
                    outlet_quantity3 = table.Column<int>(type: "integer", nullable: false),
                    outlet_quantity4 = table.Column<int>(type: "integer", nullable: false),
                    outlet_quantity5 = table.Column<int>(type: "integer", nullable: false),
                    cloud_quantity = table.Column<int>(type: "integer", nullable: false),
                    plenum_quantity = table.Column<int>(type: "integer", nullable: false),
                    has_flexible_hose = table.Column<bool>(type: "boolean", nullable: false),
                    flexible_hose_quantity = table.Column<int>(type: "integer", nullable: false),
                    has_rigid_hose = table.Column<bool>(type: "boolean", nullable: false),
                    rigid_hose_quantity = table.Column<int>(type: "integer", nullable: false),
                    remote_quantity = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ducted_units", x => x.id);
                    table.ForeignKey(
                        name: "fk_ducted_units_quotes_quote_id",
                        column: x => x.quote_id,
                        principalTable: "quotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quote_attachments",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    quote_id = table.Column<int>(type: "integer", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    file_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    uploaded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quote_attachments", x => x.id);
                    table.ForeignKey(
                        name: "fk_quote_attachments_quotes_quote_id",
                        column: x => x.quote_id,
                        principalTable: "quotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quote_products",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    quote_id = table.Column<int>(type: "integer", nullable: false),
                    product_id = table.Column<int>(type: "integer", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    quantity = table.Column<int>(type: "integer", nullable: false),
                    price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    discounted_price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    warranty = table.Column<bool>(type: "boolean", nullable: false),
                    warranty_price = table.Column<decimal>(type: "numeric(12,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quote_products", x => x.id);
                    table.ForeignKey(
                        name: "fk_quote_products_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_quote_products_quotes_quote_id",
                        column: x => x.quote_id,
                        principalTable: "quotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "quote_service_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    quote_id = table.Column<int>(type: "integer", nullable: false),
                    service_item_id = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quote_service_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_quote_service_items_quotes_quote_id",
                        column: x => x.quote_id,
                        principalTable: "quotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_quote_service_items_service_items_service_item_id",
                        column: x => x.service_item_id,
                        principalTable: "service_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "repairs",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    client_id = table.Column<int>(type: "integer", nullable: false),
                    quote_id = table.Column<int>(type: "integer", nullable: true),
                    brand = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    description = table.Column<string>(type: "text", nullable: false),
                    urgent = table.Column<bool>(type: "boolean", nullable: false),
                    predisposed = table.Column<bool>(type: "boolean", nullable: false),
                    all_inclusive = table.Column<bool>(type: "boolean", nullable: false),
                    unit_count = table.Column<int>(type: "integer", nullable: false),
                    split_count = table.Column<int>(type: "integer", nullable: false),
                    rounding = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    inserted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    installation_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    pdf_url = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_repairs", x => x.id);
                    table.ForeignKey(
                        name: "fk_repairs_clients_client_id",
                        column: x => x.client_id,
                        principalTable: "clients",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_repairs_quotes_quote_id",
                        column: x => x.quote_id,
                        principalTable: "quotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "scheduled_installations",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    quote_id = table.Column<int>(type: "integer", nullable: false),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    slot = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    report_pdf_url = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_scheduled_installations", x => x.id);
                    table.ForeignKey(
                        name: "fk_scheduled_installations_quotes_quote_id",
                        column: x => x.quote_id,
                        principalTable: "quotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "f_gas_records",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    quote_id = table.Column<int>(type: "integer", nullable: true),
                    repair_id = table.Column<int>(type: "integer", nullable: true),
                    gas_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    kilograms_of_gas = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    is_refill = table.Column<bool>(type: "boolean", nullable: false),
                    has_extra_charge = table.Column<bool>(type: "boolean", nullable: false),
                    recorded_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_f_gas_records", x => x.id);
                    table.ForeignKey(
                        name: "fk_f_gas_records_quotes_quote_id",
                        column: x => x.quote_id,
                        principalTable: "quotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_f_gas_records_repairs_repair_id",
                        column: x => x.repair_id,
                        principalTable: "repairs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "repair_schedule_entries",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    repair_id = table.Column<int>(type: "integer", nullable: false),
                    installer_id = table.Column<int>(type: "integer", nullable: true),
                    date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_repair_schedule_entries", x => x.id);
                    table.ForeignKey(
                        name: "fk_repair_schedule_entries_installers_installer_id",
                        column: x => x.installer_id,
                        principalTable: "installers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_repair_schedule_entries_repairs_repair_id",
                        column: x => x.repair_id,
                        principalTable: "repairs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "repair_service_items",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    repair_id = table.Column<int>(type: "integer", nullable: false),
                    service_item_id = table.Column<int>(type: "integer", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    unit_price = table.Column<decimal>(type: "numeric(12,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_repair_service_items", x => x.id);
                    table.ForeignKey(
                        name: "fk_repair_service_items_repairs_repair_id",
                        column: x => x.repair_id,
                        principalTable: "repairs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_repair_service_items_service_items_service_item_id",
                        column: x => x.service_item_id,
                        principalTable: "service_items",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "installation_installers",
                columns: table => new
                {
                    scheduled_installation_id = table.Column<int>(type: "integer", nullable: false),
                    installer_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_installation_installers", x => new { x.scheduled_installation_id, x.installer_id });
                    table.ForeignKey(
                        name: "fk_installation_installers_installers_installer_id",
                        column: x => x.installer_id,
                        principalTable: "installers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_installation_installers_scheduled_installations_scheduled_i",
                        column: x => x.scheduled_installation_id,
                        principalTable: "scheduled_installations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_clients_code",
                table: "clients",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_clients_referred_by_client_id",
                table: "clients",
                column: "referred_by_client_id");

            migrationBuilder.CreateIndex(
                name: "ix_clients_source_id",
                table: "clients",
                column: "source_id");

            migrationBuilder.CreateIndex(
                name: "ix_ducted_units_quote_id",
                table: "ducted_units",
                column: "quote_id");

            migrationBuilder.CreateIndex(
                name: "ix_f_gas_records_quote_id",
                table: "f_gas_records",
                column: "quote_id");

            migrationBuilder.CreateIndex(
                name: "ix_f_gas_records_repair_id",
                table: "f_gas_records",
                column: "repair_id");

            migrationBuilder.CreateIndex(
                name: "ix_installation_installers_installer_id",
                table: "installation_installers",
                column: "installer_id");

            migrationBuilder.CreateIndex(
                name: "ix_products_price_list_id",
                table: "products",
                column: "price_list_id");

            migrationBuilder.CreateIndex(
                name: "ix_quote_attachments_quote_id",
                table: "quote_attachments",
                column: "quote_id");

            migrationBuilder.CreateIndex(
                name: "ix_quote_products_product_id",
                table: "quote_products",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_quote_products_quote_id",
                table: "quote_products",
                column: "quote_id");

            migrationBuilder.CreateIndex(
                name: "ix_quote_service_items_quote_id",
                table: "quote_service_items",
                column: "quote_id");

            migrationBuilder.CreateIndex(
                name: "ix_quote_service_items_service_item_id",
                table: "quote_service_items",
                column: "service_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_quotes_client_id",
                table: "quotes",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_quotes_number_version_revision",
                table: "quotes",
                columns: new[] { "number", "version", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quotes_operator_id",
                table: "quotes",
                column: "operator_id");

            migrationBuilder.CreateIndex(
                name: "ix_quotes_parent_quote_id",
                table: "quotes",
                column: "parent_quote_id");

            migrationBuilder.CreateIndex(
                name: "ix_repair_schedule_entries_installer_id",
                table: "repair_schedule_entries",
                column: "installer_id");

            migrationBuilder.CreateIndex(
                name: "ix_repair_schedule_entries_repair_id",
                table: "repair_schedule_entries",
                column: "repair_id");

            migrationBuilder.CreateIndex(
                name: "ix_repair_service_items_repair_id",
                table: "repair_service_items",
                column: "repair_id");

            migrationBuilder.CreateIndex(
                name: "ix_repair_service_items_service_item_id",
                table: "repair_service_items",
                column: "service_item_id");

            migrationBuilder.CreateIndex(
                name: "ix_repairs_client_id",
                table: "repairs",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_repairs_quote_id",
                table: "repairs",
                column: "quote_id");

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_installations_quote_id",
                table: "scheduled_installations",
                column: "quote_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_items_product_type_id",
                table: "service_items",
                column: "product_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_users_installer_id",
                table: "users",
                column: "installer_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ducted_units");

            migrationBuilder.DropTable(
                name: "f_gas_records");

            migrationBuilder.DropTable(
                name: "installation_installers");

            migrationBuilder.DropTable(
                name: "quote_attachments");

            migrationBuilder.DropTable(
                name: "quote_products");

            migrationBuilder.DropTable(
                name: "quote_service_items");

            migrationBuilder.DropTable(
                name: "repair_schedule_entries");

            migrationBuilder.DropTable(
                name: "repair_service_items");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "scheduled_installations");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropTable(
                name: "repairs");

            migrationBuilder.DropTable(
                name: "service_items");

            migrationBuilder.DropTable(
                name: "installers");

            migrationBuilder.DropTable(
                name: "product_price_lists");

            migrationBuilder.DropTable(
                name: "quotes");

            migrationBuilder.DropTable(
                name: "product_types");

            migrationBuilder.DropTable(
                name: "clients");

            migrationBuilder.DropTable(
                name: "operators");

            migrationBuilder.DropTable(
                name: "client_sources");
        }
    }
}
