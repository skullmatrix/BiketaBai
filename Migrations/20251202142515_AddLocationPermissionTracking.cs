using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BiketaBai.Migrations
{
    /// <inheritdoc />
    public partial class AddLocationPermissionTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "location_permission_denied_at",
                table: "bookings",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "location_permission_granted",
                table: "bookings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "location_permission_denied_at",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "location_permission_granted",
                table: "bookings");
        }
    }
}
