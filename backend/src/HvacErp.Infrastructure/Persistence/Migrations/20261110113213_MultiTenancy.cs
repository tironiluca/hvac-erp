using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HvacErp.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Turns a single-company database into a multi-tenant one.
    /// <para>
    /// Adds <c>tenants</c>, <c>stores</c>, <c>tenant_memberships</c> and <c>store_access</c>; gives
    /// every business table a <c>tenant_id</c> and every store-level table a <c>store_id</c>; moves
    /// the existing data to a default tenant and store (ids 1) and each user's role into a
    /// membership of that tenant, with every existing Admin becoming a platform administrator.
    /// Codes and quote numbers become unique per tenant / per store.
    /// </para>
    /// <para>
    /// Isolation is enforced in the database twice over (see <c>MultiTenancy.Tenancy.cs</c>):
    /// composite foreign keys on <c>(tenant_id, …)</c> so no row can reference another tenant's row,
    /// and row-level security rewritten around the caller's memberships, so the Data API gives a
    /// signed-in identity nothing until a membership grants it.
    /// </para>
    /// <para>
    /// <c>Down</c> restores the single-company schema and the previous policies, and refuses to run
    /// once a second tenant exists: collapsing tenants would merge their data.
    /// </para>
    /// </summary>
    public partial class MultiTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_users_installers_installer_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_users_installer_id",
                table: "users");

            migrationBuilder.DropIndex(
                name: "ix_service_items_code",
                table: "service_items");

            migrationBuilder.DropIndex(
                name: "ix_quotes_number_version_revision",
                table: "quotes");

            migrationBuilder.DropIndex(
                name: "ix_pdf_templates_document_type",
                table: "pdf_templates");

            migrationBuilder.DropIndex(
                name: "ix_pdf_templates_document_type_name",
                table: "pdf_templates");

            migrationBuilder.DropIndex(
                name: "ix_incentive_schemes_code",
                table: "incentive_schemes");

            migrationBuilder.DropIndex(
                name: "ix_extra_types_code",
                table: "extra_types");

            migrationBuilder.DropIndex(
                name: "ix_clients_code",
                table: "clients");

            migrationBuilder.AddColumn<bool>(
                name: "is_active",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "is_platform_admin",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                table: "service_items",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "store_id",
                table: "scheduled_installations",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                table: "scheduled_installations",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "store_id",
                table: "repairs",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                table: "repairs",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                table: "repair_status_events",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                table: "repair_service_items",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                table: "repair_schedule_entries",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "store_id",
                table: "quotes",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                table: "quotes",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                table: "quote_status_events",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                table: "quote_service_items",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                table: "quote_products",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                table: "quote_incentives",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                table: "quote_extras",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                table: "quote_attachments",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                table: "product_types",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                table: "product_price_lists",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                table: "price_list_discount_tiers",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                table: "pdf_templates",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                table: "operators",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                table: "installers",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                table: "installation_installers",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                table: "incentive_schemes",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "store_id",
                table: "f_gas_records",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                table: "f_gas_records",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                table: "extra_types",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "store_id",
                table: "erp_order_exports",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                table: "erp_order_exports",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                table: "ducted_units",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                table: "ducted_unit_outlets",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                table: "clients",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.AddColumn<int>(
                name: "tenant_id",
                table: "client_sources",
                type: "integer",
                nullable: false,
                defaultValue: ExistingRowsOwner);

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    legal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    vat_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenants", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "stores",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    address = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: true),
                    city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    phone = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_stores", x => x.id);
                    table.ForeignKey(
                        name: "fk_stores_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "tenant_memberships",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    tenant_id = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    all_stores = table.Column<bool>(type: "boolean", nullable: false),
                    installer_id = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_tenant_memberships", x => x.id);
                    table.CheckConstraint("ck_tenant_memberships_role", "role IN ('Admin', 'Sales', 'Warehouse')");
                    table.ForeignKey(
                        name: "fk_tenant_memberships_installers_installer_id",
                        column: x => x.installer_id,
                        principalTable: "installers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_tenant_memberships_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_tenant_memberships_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "store_access",
                columns: table => new
                {
                    tenant_membership_id = table.Column<int>(type: "integer", nullable: false),
                    store_id = table.Column<int>(type: "integer", nullable: false),
                    tenant_id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_store_access", x => new { x.tenant_membership_id, x.store_id });
                    table.ForeignKey(
                        name: "fk_store_access_stores_store_id",
                        column: x => x.store_id,
                        principalTable: "stores",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_store_access_tenant_memberships_tenant_membership_id",
                        column: x => x.tenant_membership_id,
                        principalTable: "tenant_memberships",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Existing data moves to the default tenant and store before the old role column goes.
            migrationBuilder.Sql(BackfillSql);

            migrationBuilder.DropColumn(
                name: "installer_id",
                table: "users");

            migrationBuilder.DropColumn(
                name: "role",
                table: "users");

            migrationBuilder.CreateIndex(
                name: "ix_service_items_tenant_id_code",
                table: "service_items",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_installations_tenant_id_store_id",
                table: "scheduled_installations",
                columns: new[] { "tenant_id", "store_id" });

            migrationBuilder.CreateIndex(
                name: "ix_repairs_tenant_id_store_id",
                table: "repairs",
                columns: new[] { "tenant_id", "store_id" });

            migrationBuilder.CreateIndex(
                name: "ix_quotes_store_id_number_version_revision",
                table: "quotes",
                columns: new[] { "store_id", "number", "version", "revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quotes_tenant_id_store_id",
                table: "quotes",
                columns: new[] { "tenant_id", "store_id" });

            migrationBuilder.CreateIndex(
                name: "ix_pdf_templates_tenant_id_document_type",
                table: "pdf_templates",
                columns: new[] { "tenant_id", "document_type" },
                unique: true,
                filter: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_pdf_templates_tenant_id_document_type_name",
                table: "pdf_templates",
                columns: new[] { "tenant_id", "document_type", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_incentive_schemes_tenant_id_code",
                table: "incentive_schemes",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_f_gas_records_tenant_id_store_id",
                table: "f_gas_records",
                columns: new[] { "tenant_id", "store_id" });

            migrationBuilder.CreateIndex(
                name: "ix_extra_types_tenant_id_code",
                table: "extra_types",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_erp_order_exports_tenant_id_store_id",
                table: "erp_order_exports",
                columns: new[] { "tenant_id", "store_id" });

            migrationBuilder.CreateIndex(
                name: "ix_clients_tenant_id_code",
                table: "clients",
                columns: new[] { "tenant_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_store_access_store_id",
                table: "store_access",
                column: "store_id");

            migrationBuilder.CreateIndex(
                name: "ix_stores_tenant_id_name",
                table: "stores",
                columns: new[] { "tenant_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_memberships_installer_id",
                table: "tenant_memberships",
                column: "installer_id");

            migrationBuilder.CreateIndex(
                name: "ix_tenant_memberships_tenant_id_user_id",
                table: "tenant_memberships",
                columns: new[] { "tenant_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_tenant_memberships_user_id",
                table: "tenant_memberships",
                column: "user_id");

            ApplyTenancyConstraints(migrationBuilder);

            ApplyTenantRowLevelSecurity(migrationBuilder);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(RefuseDownWithSeveralTenantsSql);

            RemoveTenantRowLevelSecurity(migrationBuilder);

            RemoveTenancyConstraints(migrationBuilder);

            migrationBuilder.AddColumn<int>(
                name: "installer_id",
                table: "users",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "role",
                table: "users",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // Roles go back onto the profile before the memberships are dropped.
            migrationBuilder.Sql(RestoreUserRolesSql);

            migrationBuilder.DropTable(
                name: "store_access");

            migrationBuilder.DropTable(
                name: "stores");

            migrationBuilder.DropTable(
                name: "tenant_memberships");

            migrationBuilder.DropTable(
                name: "tenants");

            migrationBuilder.DropIndex(
                name: "ix_service_items_tenant_id_code",
                table: "service_items");

            migrationBuilder.DropIndex(
                name: "ix_scheduled_installations_tenant_id_store_id",
                table: "scheduled_installations");

            migrationBuilder.DropIndex(
                name: "ix_repairs_tenant_id_store_id",
                table: "repairs");

            migrationBuilder.DropIndex(
                name: "ix_quotes_store_id_number_version_revision",
                table: "quotes");

            migrationBuilder.DropIndex(
                name: "ix_quotes_tenant_id_store_id",
                table: "quotes");

            migrationBuilder.DropIndex(
                name: "ix_pdf_templates_tenant_id_document_type",
                table: "pdf_templates");

            migrationBuilder.DropIndex(
                name: "ix_pdf_templates_tenant_id_document_type_name",
                table: "pdf_templates");

            migrationBuilder.DropIndex(
                name: "ix_incentive_schemes_tenant_id_code",
                table: "incentive_schemes");

            migrationBuilder.DropIndex(
                name: "ix_f_gas_records_tenant_id_store_id",
                table: "f_gas_records");

            migrationBuilder.DropIndex(
                name: "ix_extra_types_tenant_id_code",
                table: "extra_types");

            migrationBuilder.DropIndex(
                name: "ix_erp_order_exports_tenant_id_store_id",
                table: "erp_order_exports");

            migrationBuilder.DropIndex(
                name: "ix_clients_tenant_id_code",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "is_active",
                table: "users");

            migrationBuilder.DropColumn(
                name: "is_platform_admin",
                table: "users");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "service_items");

            migrationBuilder.DropColumn(
                name: "store_id",
                table: "scheduled_installations");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "scheduled_installations");

            migrationBuilder.DropColumn(
                name: "store_id",
                table: "repairs");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "repairs");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "repair_status_events");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "repair_service_items");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "repair_schedule_entries");

            migrationBuilder.DropColumn(
                name: "store_id",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "quote_status_events");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "quote_service_items");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "quote_products");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "quote_incentives");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "quote_extras");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "quote_attachments");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "products");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "product_types");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "product_price_lists");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "price_list_discount_tiers");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "pdf_templates");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "operators");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "installers");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "installation_installers");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "incentive_schemes");

            migrationBuilder.DropColumn(
                name: "store_id",
                table: "f_gas_records");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "f_gas_records");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "extra_types");

            migrationBuilder.DropColumn(
                name: "store_id",
                table: "erp_order_exports");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "erp_order_exports");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "ducted_units");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "ducted_unit_outlets");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "clients");

            migrationBuilder.DropColumn(
                name: "tenant_id",
                table: "client_sources");

            migrationBuilder.CreateIndex(
                name: "ix_users_installer_id",
                table: "users",
                column: "installer_id");

            migrationBuilder.CreateIndex(
                name: "ix_service_items_code",
                table: "service_items",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_quotes_number_version_revision",
                table: "quotes",
                columns: new[] { "number", "version", "revision" },
                unique: true);

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

            migrationBuilder.CreateIndex(
                name: "ix_incentive_schemes_code",
                table: "incentive_schemes",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_extra_types_code",
                table: "extra_types",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_clients_code",
                table: "clients",
                column: "code",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_users_installers_installer_id",
                table: "users",
                column: "installer_id",
                principalTable: "installers",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            RestoreLegacyRowLevelSecurity(migrationBuilder);
        }
    }
}
