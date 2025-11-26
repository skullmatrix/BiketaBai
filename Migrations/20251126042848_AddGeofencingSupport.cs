using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BiketaBai.Migrations
{
    /// <inheritdoc />
    public partial class AddGeofencingSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "geofence_radius_km",
                table: "users",
                type: "decimal(65,30)",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "store_latitude",
                table: "users",
                type: "double",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "store_longitude",
                table: "users",
                type: "double",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "location_tracking",
                columns: table => new
                {
                    tracking_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    booking_id = table.Column<int>(type: "int", nullable: false),
                    latitude = table.Column<double>(type: "double", nullable: false),
                    longitude = table.Column<double>(type: "double", nullable: false),
                    distance_from_store_km = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: true),
                    is_within_geofence = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    tracked_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_location_tracking", x => x.tracking_id);
                    table.ForeignKey(
                        name: "FK_location_tracking_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "booking_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_location_tracking_booking_id",
                table: "location_tracking",
                column: "booking_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "location_tracking");

            migrationBuilder.DropColumn(
                name: "geofence_radius_km",
                table: "users");

            migrationBuilder.DropColumn(
                name: "store_latitude",
                table: "users");

            migrationBuilder.DropColumn(
                name: "store_longitude",
                table: "users");
        }
    }
}
