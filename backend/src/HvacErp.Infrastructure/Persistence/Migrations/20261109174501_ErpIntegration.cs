using System;
using System.Text;
using Microsoft.EntityFrameworkCore.Migrations;
using static HvacErp.Infrastructure.Persistence.Migrations.RowLevelSecuritySql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HvacErp.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Main-ERP integration: <c>erp_order_exports</c> is the outbox of confirmed quotes to send (one per
    /// quote), and <c>mock_erp_orders</c> the storage of the development mock ERP. Both get row-level
    /// security; the mock's table has no policies, so the Data API cannot see it at all.
    /// </summary>
    public partial class ErpIntegration : Migration
    {
        /// <summary>The tables this migration creates; all get row-level security (checked by the test suite).</summary>
        internal static readonly string[] SecuredTables = ["erp_order_exports", "mock_erp_orders"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "erp_order_exports",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    quote_id = table.Column<int>(type: "integer", nullable: false),
                    connector = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    next_attempt_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    erp_order_number = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    last_error = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_erp_order_exports", x => x.id);
                    table.CheckConstraint("ck_erp_order_exports_status", "status IN ('Pending', 'Sent', 'Failed')");
                    table.ForeignKey(
                        name: "fk_erp_order_exports_quotes_quote_id",
                        column: x => x.quote_id,
                        principalTable: "quotes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "mock_erp_orders",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    order_number = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    external_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    customer_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    final_price = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    line_count = table.Column<int>(type: "integer", nullable: false),
                    received_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    payload_json = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_mock_erp_orders", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_erp_order_exports_quote_id",
                table: "erp_order_exports",
                column: "quote_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_erp_order_exports_status_next_attempt_at",
                table: "erp_order_exports",
                columns: new[] { "status", "next_attempt_at" });

            migrationBuilder.CreateIndex(
                name: "ix_mock_erp_orders_external_reference",
                table: "mock_erp_orders",
                column: "external_reference",
                unique: true);

            foreach (var table in SecuredTables)
            {
                migrationBuilder.Sql(EnableFor(table));
            }

            var policies = new StringBuilder();
            AppendReadPolicy(policies, "erp_order_exports");
            AppendWritePolicies(policies, "erp_order_exports", IsSalesOrAdmin, "sales_or_admin");
            migrationBuilder.Sql(OnSupabaseOnly(policies.ToString()));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "erp_order_exports");

            migrationBuilder.DropTable(
                name: "mock_erp_orders");
        }
    }
}
