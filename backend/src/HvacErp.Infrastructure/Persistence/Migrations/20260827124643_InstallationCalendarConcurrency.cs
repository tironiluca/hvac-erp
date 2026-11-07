using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace HvacErp.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InstallationCalendarConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "concurrency_token",
                table: "scheduled_installations",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.AddColumn<DateTime>(
                name: "updated_at",
                table: "scheduled_installations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "updated_by",
                table: "scheduled_installations",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            // Give existing rows a real, distinct token so the calendar's optimistic-concurrency
            // check is meaningful from the first reschedule rather than after a save-once warm-up.
            migrationBuilder.Sql("UPDATE scheduled_installations SET concurrency_token = gen_random_uuid();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "concurrency_token",
                table: "scheduled_installations");

            migrationBuilder.DropColumn(
                name: "updated_at",
                table: "scheduled_installations");

            migrationBuilder.DropColumn(
                name: "updated_by",
                table: "scheduled_installations");
        }
    }
}
