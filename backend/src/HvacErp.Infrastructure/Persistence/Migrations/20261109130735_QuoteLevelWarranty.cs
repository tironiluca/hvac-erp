using System.Text;
using Microsoft.EntityFrameworkCore.Migrations;
using static HvacErp.Infrastructure.Persistence.Migrations.RowLevelSecuritySql;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace HvacErp.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The extended warranty is per quote, not per unit: line-level schemes become quote amounts
    /// charged with the equipment before VAT, and staff choose per quote between a fixed € amount and a
    /// percentage of the equipment list total (<c>quote_incentives.mode</c> / <c>value</c>). Each
    /// quote's existing line amounts are summed into one fixed amount, so no total changes;
    /// <c>quote_product_incentives</c> is dropped.
    /// </summary>
    public partial class QuoteLevelWarranty : Migration
    {
        /// <summary>Tables this migration drops (the RLS coverage test subtracts them).</summary>
        internal static readonly string[] DroppedTables = ["quote_product_incentives"];

        private const string UpSql = """
            -- Quote amounts already on quotes (fees, deductions) keep their amount as a fixed value.
            UPDATE quote_incentives qi SET mode = 'Fixed', value = qi.amount
            FROM incentive_schemes s
            WHERE s.id = qi.incentive_scheme_id AND s.kind = 'QuoteAmount';

            -- Line-level schemes (the warranty) become quote-level, charged with the equipment before VAT.
            -- A fixed default of €100 is proposed when staff switch a quote to a fixed amount.
            UPDATE incentive_schemes
            SET kind = 'QuoteAmount', charge_before_vat = true, default_amount = COALESCE(default_amount, 100),
                description = CASE
                    WHEN description LIKE '%per unità%' THEN
                        'Garanzia estesa per l''intero preventivo: di default l''8% del listino delle macchine, oppure un importo fisso.'
                    WHEN description LIKE '%per unit%' THEN
                        'Extended warranty for the whole quote: 8% of the equipment list total by default, or a fixed amount.'
                    ELSE description END
            WHERE kind = 'LineAmount';

            -- Each quote's line amounts become one fixed amount on the quote, so totals do not move.
            INSERT INTO quote_incentives (quote_id, incentive_scheme_id, mode, value, amount, confirmed)
            SELECT qp.quote_id, qpi.incentive_scheme_id, 'Fixed', SUM(qpi.amount), SUM(qpi.amount), false
            FROM quote_product_incentives qpi
            JOIN quote_products qp ON qp.id = qpi.quote_product_id
            GROUP BY qp.quote_id, qpi.incentive_scheme_id
            ON CONFLICT (quote_id, incentive_scheme_id) DO NOTHING;
            """;

        // Down: schemes charged before VAT go back to per line; each quote's amount lands on its first
        // product line as a fixed amount per unit (the per-line split is not recoverable).
        private const string DownSql = """
            UPDATE incentive_schemes SET kind = 'LineAmount' WHERE kind = 'QuoteAmount' AND charge_before_vat;

            INSERT INTO quote_product_incentives (quote_product_id, incentive_scheme_id, mode, value, amount)
            SELECT first_line.id, qi.incentive_scheme_id, 'Fixed',
                ROUND(COALESCE(qi.amount, 0) / first_line.quantity, 2), COALESCE(qi.amount, 0)
            FROM quote_incentives qi
            JOIN incentive_schemes s ON s.id = qi.incentive_scheme_id AND s.kind = 'LineAmount'
            JOIN LATERAL (
                SELECT qp.id, qp.quantity FROM quote_products qp WHERE qp.quote_id = qi.quote_id ORDER BY qp.id LIMIT 1
            ) first_line ON true;

            DELETE FROM quote_incentives qi USING incentive_schemes s
            WHERE s.id = qi.incentive_scheme_id AND s.kind = 'LineAmount';
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_incentive_schemes_kind",
                table: "incentive_schemes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_incentive_schemes_line_amount",
                table: "incentive_schemes");

            migrationBuilder.AddColumn<string>(
                name: "mode",
                table: "quote_incentives",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "value",
                table: "quote_incentives",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "charge_before_vat",
                table: "incentive_schemes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql(UpSql);

            migrationBuilder.DropTable(
                name: "quote_product_incentives");

            migrationBuilder.AddCheckConstraint(
                name: "ck_quote_incentives_mode",
                table: "quote_incentives",
                sql: "mode IS NULL OR mode IN ('Fixed', 'Percent')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_quote_incentives_value_non_negative",
                table: "quote_incentives",
                sql: "value IS NULL OR value >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_incentive_schemes_kind",
                table: "incentive_schemes",
                sql: "kind IN ('QuoteAmount', 'InvoiceDiscount')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_quote_incentives_mode",
                table: "quote_incentives");

            migrationBuilder.DropCheckConstraint(
                name: "ck_quote_incentives_value_non_negative",
                table: "quote_incentives");

            migrationBuilder.DropCheckConstraint(
                name: "ck_incentive_schemes_kind",
                table: "incentive_schemes");

            migrationBuilder.CreateTable(
                name: "quote_product_incentives",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    incentive_scheme_id = table.Column<int>(type: "integer", nullable: false),
                    quote_product_id = table.Column<int>(type: "integer", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    mode = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    value = table.Column<decimal>(type: "numeric(12,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_quote_product_incentives", x => x.id);
                    table.CheckConstraint("ck_quote_product_incentives_mode", "mode IN ('Percent', 'Fixed')");
                    table.CheckConstraint("ck_quote_product_incentives_value_non_negative", "value >= 0");
                    table.ForeignKey(
                        name: "fk_quote_product_incentives_incentive_schemes_incentive_scheme",
                        column: x => x.incentive_scheme_id,
                        principalTable: "incentive_schemes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_quote_product_incentives_quote_products_quote_product_id",
                        column: x => x.quote_product_id,
                        principalTable: "quote_products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_quote_product_incentives_incentive_scheme_id",
                table: "quote_product_incentives",
                column: "incentive_scheme_id");

            migrationBuilder.CreateIndex(
                name: "ix_quote_product_incentives_quote_product_id_incentive_scheme_",
                table: "quote_product_incentives",
                columns: new[] { "quote_product_id", "incentive_scheme_id" },
                unique: true);
            migrationBuilder.Sql(DownSql);
            migrationBuilder.Sql(EnableFor("quote_product_incentives"));
            var policies = new StringBuilder();
            AppendReadPolicy(policies, "quote_product_incentives");
            AppendWritePolicies(policies, "quote_product_incentives", IsSalesOrAdmin, "sales_or_admin");
            migrationBuilder.Sql(OnSupabaseOnly(policies.ToString()));

            migrationBuilder.AddCheckConstraint(
                name: "ck_incentive_schemes_kind",
                table: "incentive_schemes",
                sql: "kind IN ('QuoteAmount', 'LineAmount', 'InvoiceDiscount')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_incentive_schemes_line_amount",
                table: "incentive_schemes",
                sql: "kind <> 'LineAmount' OR default_amount IS NOT NULL OR percent IS NOT NULL");

            migrationBuilder.DropColumn(
                name: "mode",
                table: "quote_incentives");

            migrationBuilder.DropColumn(
                name: "value",
                table: "quote_incentives");

            migrationBuilder.DropColumn(
                name: "charge_before_vat",
                table: "incentive_schemes");
        }
    }
}
