using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HvacErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase8bQuoteFiscalAndSiteFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "installation_address",
                table: "quotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "installation_city",
                table: "quotes",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "raee_eligible",
                table: "quotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "raee_price",
                table: "quotes",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "sf_confirmed",
                table: "quotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "sf_price",
                table: "quotes",
                type: "numeric(12,2)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "sf_requested",
                table: "quotes",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "installation_address",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "installation_city",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "raee_eligible",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "raee_price",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "sf_confirmed",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "sf_price",
                table: "quotes");

            migrationBuilder.DropColumn(
                name: "sf_requested",
                table: "quotes");
        }
    }
}
