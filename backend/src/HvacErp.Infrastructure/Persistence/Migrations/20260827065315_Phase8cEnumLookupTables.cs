using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HvacErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase8cEnumLookupTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "slot",
                table: "scheduled_installations",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "vat",
                table: "quotes",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "department",
                table: "quotes",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "current_status",
                table: "quotes",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "quote_status_events",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "system_type",
                table: "ducted_unit_outlets",
                type: "character varying(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.CreateTable(
                name: "day_slots",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_day_slots", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "duct_system_types",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_duct_system_types", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "quote_departments",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quote_departments", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "quote_statuses",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    is_terminal = table.Column<bool>(type: "boolean", nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quote_statuses", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "repair_statuses",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    legacy_ordinal = table.Column<int>(type: "integer", nullable: false),
                    is_terminal = table.Column<bool>(type: "boolean", nullable: false),
                    requires_direct_assistance = table.Column<bool>(type: "boolean", nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_repair_statuses", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "vat_options",
                columns: table => new
                {
                    code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    blend_shape = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    machines_multiplier = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    installation_multiplier = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    pdf_label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    label = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    color = table.Column<string>(type: "character varying(9)", maxLength: 9, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_vat_options", x => x.code);
                });

            migrationBuilder.CreateTable(
                name: "repair_status_events",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    repair_id = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    occurred_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    changed_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_repair_status_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_repair_status_events_repair_statuses_status",
                        column: x => x.status,
                        principalTable: "repair_statuses",
                        principalColumn: "code",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_repair_status_events_repairs_repair_id",
                        column: x => x.repair_id,
                        principalTable: "repairs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            // Seed the lookup rows before the FK constraints below validate existing data.
            // Kept in sync with DevDataSeeder.SeedLookupTablesAsync; ON CONFLICT keeps this idempotent.
            migrationBuilder.Sql(@"
                INSERT INTO vat_options (code, label, description, color, sort_order, active, blend_shape, machines_multiplier, installation_multiplier, pdf_label) VALUES
                    ('Standard22', 'Standard VAT 22%', NULL, NULL, 10, true, 'Split', 1.22, 2.20, 'VAT included (22%)'),
                    ('Reduced10',  'Reduced VAT 10%', NULL, NULL, 20, true, 'Flat', 1.10, 1.10, 'Reduced VAT (10%)'),
                    ('Exempt',     'VAT exempt',        NULL, NULL, 30, true, 'None', 1.00, 1.00, 'VAT excluded')
                ON CONFLICT (code) DO NOTHING;

                INSERT INTO quote_departments (code, label, description, color, sort_order, active) VALUES
                    ('Store', 'Store', NULL, NULL, 10, true),
                    ('Field', 'Field', NULL, NULL, 20, true)
                ON CONFLICT (code) DO NOTHING;

                INSERT INTO quote_statuses (code, label, description, color, sort_order, active, is_terminal) VALUES
                    ('Draft',      'Draft',             NULL, NULL, 10, true, false),
                    ('SurveyDone', 'Survey done'      , NULL, NULL, 20, true, false),
                    ('Recalled',   'Called back',       NULL, NULL, 30, true, false),
                    ('Confirmed',  'Confirmed',         NULL, NULL, 40, true, true)
                ON CONFLICT (code) DO NOTHING;

                INSERT INTO duct_system_types (code, label, description, color, sort_order, active) VALUES
                    ('Standard', 'Standard', NULL, NULL, 10, true),
                    ('Airzone',  'Airzone',  NULL, NULL, 20, true)
                ON CONFLICT (code) DO NOTHING;

                INSERT INTO day_slots (code, label, description, color, sort_order, active) VALUES
                    ('Morning',   'Morning',    NULL, NULL, 10, true),
                    ('Afternoon', 'Afternoon' , NULL, NULL, 20, true),
                    ('FullDay',   'Full day',   NULL, NULL, 30, true)
                ON CONFLICT (code) DO NOTHING;

                INSERT INTO repair_statuses (code, label, description, color, sort_order, active, legacy_ordinal, is_terminal, requires_direct_assistance) VALUES
                    ('New',                 'New',                        NULL,                                            NULL,      10,  true, 0, false, false),
                    ('Urgent',              'Urgent',                     NULL,                                            '#C62828', 20,  true, 1, false, false),
                    ('Scheduled',           'Scheduled',                  'Assigned, work not started yet',                NULL,      30,  true, 2, false, false),
                    ('InProgress',          'In progress',                NULL,                                            NULL,      40,  true, 3, false, false),
                    ('AwaitingPayment',     'Awaiting payment',           NULL,                                            NULL,      50,  true, 4, false, false),
                    ('PendingOffSeason',    'On hold until the season',   NULL,                                            NULL,      60,  true, 5, false, false),
                    ('ClosedByInstallers',  'Closed by installers',       NULL,                                            NULL,      70,  true, 6, true,  false),
                    ('Closed',              'Closed',                     NULL,                                            '#616161', 80,  true, 7, true,  false),
                    ('AssistanceRequested', 'Manufacturer support requested', NULL,                                            '#F9A825', 90,  true, 8, false, true),
                    ('AssistanceContacted', 'Manufacturer support contacted', NULL,                                            '#F9A825', 100, true, 9, false, true)
                ON CONFLICT (code) DO NOTHING;

                -- Reconcile pre-8c repair rows: old best-effort labels -> 8a's confirmed names, by legacy ordinal.
                UPDATE repairs SET status = CASE status
                    WHEN 'InProgress'            THEN 'Scheduled'
                    WHEN 'WaitingForParts'       THEN 'InProgress'
                    WHEN 'Scheduled'             THEN 'AwaitingPayment'
                    WHEN 'WaitingForClient'      THEN 'PendingOffSeason'
                    WHEN 'OnHold'                THEN 'ClosedByInstallers'
                    WHEN 'ReadyForPickup'        THEN 'AssistanceRequested'
                    WHEN 'NeedsDirectAssistance' THEN 'AssistanceContacted'
                    ELSE status
                END
                WHERE status IN ('InProgress','WaitingForParts','Scheduled','WaitingForClient','OnHold','ReadyForPickup','NeedsDirectAssistance');
            ");

            migrationBuilder.CreateIndex(
                name: "ix_scheduled_installations_slot",
                table: "scheduled_installations",
                column: "slot");

            migrationBuilder.CreateIndex(
                name: "ix_repairs_status",
                table: "repairs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_quotes_current_status",
                table: "quotes",
                column: "current_status");

            migrationBuilder.CreateIndex(
                name: "ix_quotes_department",
                table: "quotes",
                column: "department");

            migrationBuilder.CreateIndex(
                name: "ix_quotes_vat",
                table: "quotes",
                column: "vat");

            migrationBuilder.CreateIndex(
                name: "ix_quote_status_events_status",
                table: "quote_status_events",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_ducted_unit_outlets_system_type",
                table: "ducted_unit_outlets",
                column: "system_type");

            migrationBuilder.CreateIndex(
                name: "ix_repair_status_events_repair_id_occurred_at",
                table: "repair_status_events",
                columns: new[] { "repair_id", "occurred_at" });

            migrationBuilder.CreateIndex(
                name: "ix_repair_status_events_status",
                table: "repair_status_events",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_repair_statuses_legacy_ordinal",
                table: "repair_statuses",
                column: "legacy_ordinal",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_ducted_unit_outlets_duct_system_types_system_type",
                table: "ducted_unit_outlets",
                column: "system_type",
                principalTable: "duct_system_types",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_quote_status_events_quote_statuses_status",
                table: "quote_status_events",
                column: "status",
                principalTable: "quote_statuses",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_quotes_quote_departments_department",
                table: "quotes",
                column: "department",
                principalTable: "quote_departments",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_quotes_quote_statuses_current_status",
                table: "quotes",
                column: "current_status",
                principalTable: "quote_statuses",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_quotes_vat_options_vat",
                table: "quotes",
                column: "vat",
                principalTable: "vat_options",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_repairs_repair_statuses_status",
                table: "repairs",
                column: "status",
                principalTable: "repair_statuses",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_scheduled_installations_day_slots_slot",
                table: "scheduled_installations",
                column: "slot",
                principalTable: "day_slots",
                principalColumn: "code",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_ducted_unit_outlets_duct_system_types_system_type",
                table: "ducted_unit_outlets");

            migrationBuilder.DropForeignKey(
                name: "fk_quote_status_events_quote_statuses_status",
                table: "quote_status_events");

            migrationBuilder.DropForeignKey(
                name: "fk_quotes_quote_departments_department",
                table: "quotes");

            migrationBuilder.DropForeignKey(
                name: "fk_quotes_quote_statuses_current_status",
                table: "quotes");

            migrationBuilder.DropForeignKey(
                name: "fk_quotes_vat_options_vat",
                table: "quotes");

            migrationBuilder.DropForeignKey(
                name: "fk_repairs_repair_statuses_status",
                table: "repairs");

            migrationBuilder.DropForeignKey(
                name: "fk_scheduled_installations_day_slots_slot",
                table: "scheduled_installations");

            // Inverse of the Up() reconciliation — 8a names back to the pre-8c best-effort labels
            // (run after the repair_statuses FK is gone so the old names are accepted again).
            migrationBuilder.Sql(@"
                UPDATE repairs SET status = CASE status
                    WHEN 'Scheduled'           THEN 'InProgress'
                    WHEN 'InProgress'          THEN 'WaitingForParts'
                    WHEN 'AwaitingPayment'     THEN 'Scheduled'
                    WHEN 'PendingOffSeason'    THEN 'WaitingForClient'
                    WHEN 'ClosedByInstallers'  THEN 'OnHold'
                    WHEN 'AssistanceRequested' THEN 'ReadyForPickup'
                    WHEN 'AssistanceContacted' THEN 'NeedsDirectAssistance'
                    ELSE status
                END
                WHERE status IN ('Scheduled','InProgress','AwaitingPayment','PendingOffSeason','ClosedByInstallers','AssistanceRequested','AssistanceContacted');
            ");

            migrationBuilder.DropTable(
                name: "day_slots");

            migrationBuilder.DropTable(
                name: "duct_system_types");

            migrationBuilder.DropTable(
                name: "quote_departments");

            migrationBuilder.DropTable(
                name: "quote_statuses");

            migrationBuilder.DropTable(
                name: "repair_status_events");

            migrationBuilder.DropTable(
                name: "vat_options");

            migrationBuilder.DropTable(
                name: "repair_statuses");

            migrationBuilder.DropIndex(
                name: "ix_scheduled_installations_slot",
                table: "scheduled_installations");

            migrationBuilder.DropIndex(
                name: "ix_repairs_status",
                table: "repairs");

            migrationBuilder.DropIndex(
                name: "ix_quotes_current_status",
                table: "quotes");

            migrationBuilder.DropIndex(
                name: "ix_quotes_department",
                table: "quotes");

            migrationBuilder.DropIndex(
                name: "ix_quotes_vat",
                table: "quotes");

            migrationBuilder.DropIndex(
                name: "ix_quote_status_events_status",
                table: "quote_status_events");

            migrationBuilder.DropIndex(
                name: "ix_ducted_unit_outlets_system_type",
                table: "ducted_unit_outlets");

            migrationBuilder.AlterColumn<string>(
                name: "slot",
                table: "scheduled_installations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "vat",
                table: "quotes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "department",
                table: "quotes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "current_status",
                table: "quotes",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "status",
                table: "quote_status_events",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "system_type",
                table: "ducted_unit_outlets",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(30)",
                oldMaxLength: 30);
        }
    }
}
