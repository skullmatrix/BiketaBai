using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BiketaBai.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "store_id",
                table: "bikes",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "stores",
                columns: table => new
                {
                    store_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    owner_id = table.Column<int>(type: "int", nullable: false),
                    store_name = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    store_address = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    store_latitude = table.Column<double>(type: "double", nullable: true),
                    store_longitude = table.Column<double>(type: "double", nullable: true),
                    geofence_radius_km = table.Column<decimal>(type: "decimal(65,30)", nullable: true),
                    is_primary = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    is_deleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    deleted_at = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_stores", x => x.store_id);
                    table.ForeignKey(
                        name: "FK_stores_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_payments_owner_verified_by",
                table: "payments",
                column: "owner_verified_by");

            migrationBuilder.CreateIndex(
                name: "IX_bikes_store_id",
                table: "bikes",
                column: "store_id");

            migrationBuilder.CreateIndex(
                name: "IX_stores_owner_id",
                table: "stores",
                column: "owner_id");

            migrationBuilder.AddForeignKey(
                name: "FK_bikes_stores_store_id",
                table: "bikes",
                column: "store_id",
                principalTable: "stores",
                principalColumn: "store_id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_users_owner_verified_by",
                table: "payments",
                column: "owner_verified_by",
                principalTable: "users",
                principalColumn: "user_id",
                onDelete: ReferentialAction.Restrict);

            // Data Migration: Move store data from users to stores table
            migrationBuilder.Sql(@"
                INSERT INTO stores (owner_id, store_name, store_address, store_latitude, store_longitude, geofence_radius_km, is_primary, is_active, created_at, updated_at, is_deleted, deleted_at)
                SELECT 
                    user_id,
                    COALESCE(store_name, 'My Store'),
                    COALESCE(store_address, ''),
                    store_latitude,
                    store_longitude,
                    geofence_radius_km,
                    true,
                    true,
                    created_at,
                    updated_at,
                    is_deleted,
                    deleted_at
                FROM users
                WHERE is_owner = true 
                    AND (store_name IS NOT NULL OR store_address IS NOT NULL)
                    AND user_id NOT IN (SELECT owner_id FROM stores);
            ");

            // Link bikes to their owner's primary store
            migrationBuilder.Sql(@"
                UPDATE bikes b
                INNER JOIN stores s ON b.owner_id = s.owner_id AND s.is_primary = true
                SET b.store_id = s.store_id
                WHERE b.store_id IS NULL;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bikes_stores_store_id",
                table: "bikes");

            migrationBuilder.DropForeignKey(
                name: "FK_payments_users_owner_verified_by",
                table: "payments");

            migrationBuilder.DropTable(
                name: "stores");

            migrationBuilder.DropIndex(
                name: "IX_payments_owner_verified_by",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_bikes_store_id",
                table: "bikes");

            migrationBuilder.DropColumn(
                name: "store_id",
                table: "bikes");
        }
    }
}
