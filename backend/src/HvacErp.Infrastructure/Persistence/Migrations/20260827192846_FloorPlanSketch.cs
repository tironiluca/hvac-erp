using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HvacErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class FloorPlanSketch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "floor_plan_sketch_json",
                table: "scheduled_installations",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "floor_plan_sketch_json",
                table: "repairs",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "floor_plan_sketch_json",
                table: "quotes",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "floor_plan_sketch_json",
                table: "scheduled_installations");

            migrationBuilder.DropColumn(
                name: "floor_plan_sketch_json",
                table: "repairs");

            migrationBuilder.DropColumn(
                name: "floor_plan_sketch_json",
                table: "quotes");
        }
    }
}
