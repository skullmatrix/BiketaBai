using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BiketaBai.Migrations
{
    /// <inheritdoc />
    public partial class AddLostBikeReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_reported_lost",
                table: "bookings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "lost_report_notes",
                table: "bookings",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTime>(
                name: "reported_lost_at",
                table: "bookings",
                type: "datetime(6)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_reported_lost",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "lost_report_notes",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "reported_lost_at",
                table: "bookings");
        }
    }
}
