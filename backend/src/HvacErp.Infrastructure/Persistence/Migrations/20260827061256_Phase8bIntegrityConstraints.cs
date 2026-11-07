using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HvacErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase8bIntegrityConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Purge F-Gas rows already orphaned by a SetNull parent delete (both FKs null): they are
            // unreachable — the app only queries F-Gas records by quote_id / repair_id — and would
            // fail the ck_fgas_records_single_parent check added below. The FKs become Cascade here so
            // this can't recur.
            migrationBuilder.Sql("DELETE FROM f_gas_records WHERE quote_id IS NULL AND repair_id IS NULL;");

            migrationBuilder.DropForeignKey(
                name: "fk_f_gas_records_quotes_quote_id",
                table: "f_gas_records");

            migrationBuilder.DropForeignKey(
                name: "fk_f_gas_records_repairs_repair_id",
                table: "f_gas_records");

            migrationBuilder.DropIndex(
                name: "ix_quote_extras_quote_id",
                table: "quote_extras");

            migrationBuilder.DropIndex(
                name: "ix_ducted_unit_outlets_ducted_unit_id",
                table: "ducted_unit_outlets");

            migrationBuilder.AddCheckConstraint(
                name: "ck_quote_products_quantity_positive",
                table: "quote_products",
                sql: "quantity >= 1");

            migrationBuilder.CreateIndex(
                name: "ix_quote_extras_quote_id_extra_type_id",
                table: "quote_extras",
                columns: new[] { "quote_id", "extra_type_id" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_quote_extras_quantity_non_negative",
                table: "quote_extras",
                sql: "quantity >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_price_list_discount_tiers_percentage_range",
                table: "price_list_discount_tiers",
                sql: "percentage >= 0 AND percentage <= 100");

            migrationBuilder.AddCheckConstraint(
                name: "ck_fgas_records_single_parent",
                table: "f_gas_records",
                sql: "(quote_id IS NULL) <> (repair_id IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_ducted_unit_outlets_ducted_unit_id_position",
                table: "ducted_unit_outlets",
                columns: new[] { "ducted_unit_id", "position" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_ducted_unit_outlets_position_positive",
                table: "ducted_unit_outlets",
                sql: "position >= 1");

            migrationBuilder.AddForeignKey(
                name: "fk_f_gas_records_quotes_quote_id",
                table: "f_gas_records",
                column: "quote_id",
                principalTable: "quotes",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "fk_f_gas_records_repairs_repair_id",
                table: "f_gas_records",
                column: "repair_id",
                principalTable: "repairs",
                principalColumn: "id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_f_gas_records_quotes_quote_id",
                table: "f_gas_records");

            migrationBuilder.DropForeignKey(
                name: "fk_f_gas_records_repairs_repair_id",
                table: "f_gas_records");

            migrationBuilder.DropCheckConstraint(
                name: "ck_quote_products_quantity_positive",
                table: "quote_products");

            migrationBuilder.DropIndex(
                name: "ix_quote_extras_quote_id_extra_type_id",
                table: "quote_extras");

            migrationBuilder.DropCheckConstraint(
                name: "ck_quote_extras_quantity_non_negative",
                table: "quote_extras");

            migrationBuilder.DropCheckConstraint(
                name: "ck_price_list_discount_tiers_percentage_range",
                table: "price_list_discount_tiers");

            migrationBuilder.DropCheckConstraint(
                name: "ck_fgas_records_single_parent",
                table: "f_gas_records");

            migrationBuilder.DropIndex(
                name: "ix_ducted_unit_outlets_ducted_unit_id_position",
                table: "ducted_unit_outlets");

            migrationBuilder.DropCheckConstraint(
                name: "ck_ducted_unit_outlets_position_positive",
                table: "ducted_unit_outlets");

            migrationBuilder.CreateIndex(
                name: "ix_quote_extras_quote_id",
                table: "quote_extras",
                column: "quote_id");

            migrationBuilder.CreateIndex(
                name: "ix_ducted_unit_outlets_ducted_unit_id",
                table: "ducted_unit_outlets",
                column: "ducted_unit_id");

            migrationBuilder.AddForeignKey(
                name: "fk_f_gas_records_quotes_quote_id",
                table: "f_gas_records",
                column: "quote_id",
                principalTable: "quotes",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_f_gas_records_repairs_repair_id",
                table: "f_gas_records",
                column: "repair_id",
                principalTable: "repairs",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }
    }
}
