using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BiketaBai.Migrations
{
    /// <inheritdoc />
    public partial class RevisionsUpdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "terms_acknowledged",
                table: "bookings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "terms_acknowledged_at",
                table: "bookings",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "renter_flags",
                columns: table => new
                {
                    flag_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    booking_id = table.Column<int>(type: "int", nullable: false),
                    owner_id = table.Column<int>(type: "int", nullable: false),
                    renter_id = table.Column<int>(type: "int", nullable: false),
                    flag_reason = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    flag_description = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    is_resolved = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    resolved_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_renter_flags", x => x.flag_id);
                    table.ForeignKey(
                        name: "FK_renter_flags_bookings_booking_id",
                        column: x => x.booking_id,
                        principalTable: "bookings",
                        principalColumn: "booking_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_renter_flags_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_renter_flags_users_renter_id",
                        column: x => x.renter_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_renter_flags_booking_id",
                table: "renter_flags",
                column: "booking_id");

            migrationBuilder.CreateIndex(
                name: "IX_renter_flags_owner_id",
                table: "renter_flags",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_renter_flags_renter_id",
                table: "renter_flags",
                column: "renter_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "renter_flags");

            migrationBuilder.DropColumn(
                name: "terms_acknowledged",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "terms_acknowledged_at",
                table: "bookings");
        }
    }
}
