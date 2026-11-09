using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HvacErp.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Line-level incentives (e.g. the warranty) are priced per line as staff choose: a percentage of
    /// the list unit price or a fixed amount per unit. Existing lines take their scheme's default
    /// (its percentage when it has one, else its fixed amount); their stored amounts are unchanged
    /// until the quote is saved again.
    /// </summary>
    public partial class LineIncentiveMode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "mode",
                table: "quote_product_incentives",
                type: "character varying(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<decimal>(
                name: "value",
                table: "quote_product_incentives",
                type: "numeric(12,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.Sql("""
                UPDATE quote_product_incentives i
                SET mode = CASE WHEN s.percent IS NOT NULL THEN 'Percent' ELSE 'Fixed' END,
                    value = COALESCE(s.percent, s.default_amount, 0)
                FROM incentive_schemes s
                WHERE s.id = i.incentive_scheme_id;
                """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_quote_product_incentives_mode",
                table: "quote_product_incentives",
                sql: "mode IN ('Percent', 'Fixed')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_quote_product_incentives_value_non_negative",
                table: "quote_product_incentives",
                sql: "value >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_quote_product_incentives_mode",
                table: "quote_product_incentives");

            migrationBuilder.DropCheckConstraint(
                name: "ck_quote_product_incentives_value_non_negative",
                table: "quote_product_incentives");

            migrationBuilder.DropColumn(
                name: "mode",
                table: "quote_product_incentives");

            migrationBuilder.DropColumn(
                name: "value",
                table: "quote_product_incentives");
        }
    }
}
