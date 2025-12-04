using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BiketaBai.Migrations
{
    /// <inheritdoc />
    public partial class RemoveWalletAndLookupTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_bikes_availability_statuses_availability_status_id",
                table: "bikes");

            migrationBuilder.DropForeignKey(
                name: "FK_bookings_booking_statuses_booking_status_id",
                table: "bookings");

            migrationBuilder.DropForeignKey(
                name: "FK_payments_payment_methods_payment_method_id",
                table: "payments");

            migrationBuilder.DropTable(
                name: "availability_statuses");

            migrationBuilder.DropTable(
                name: "booking_statuses");

            migrationBuilder.DropTable(
                name: "credit_transactions");

            migrationBuilder.DropTable(
                name: "payment_methods");

            migrationBuilder.DropTable(
                name: "transaction_types");

            migrationBuilder.DropTable(
                name: "wallets");

            migrationBuilder.DropIndex(
                name: "IX_payments_payment_method_id",
                table: "payments");

            migrationBuilder.DropIndex(
                name: "IX_bookings_booking_status_id",
                table: "bookings");

            migrationBuilder.DropIndex(
                name: "IX_bikes_availability_status_id",
                table: "bikes");

            migrationBuilder.DropColumn(
                name: "geofence_radius_km",
                table: "users");

            migrationBuilder.DropColumn(
                name: "store_address",
                table: "users");

            migrationBuilder.DropColumn(
                name: "store_latitude",
                table: "users");

            migrationBuilder.DropColumn(
                name: "store_longitude",
                table: "users");

            migrationBuilder.DropColumn(
                name: "store_name",
                table: "users");

            migrationBuilder.DropColumn(
                name: "payment_method_id",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "booking_status_id",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "availability_status_id",
                table: "bikes");

            migrationBuilder.AddColumn<string>(
                name: "payment_method",
                table: "payments",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "booking_status",
                table: "bookings",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "availability_status",
                table: "bikes",
                type: "varchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_booking_status",
                table: "bookings",
                column: "booking_status");

            migrationBuilder.CreateIndex(
                name: "IX_bikes_availability_status",
                table: "bikes",
                column: "availability_status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_bookings_booking_status",
                table: "bookings");

            migrationBuilder.DropIndex(
                name: "IX_bikes_availability_status",
                table: "bikes");

            migrationBuilder.DropColumn(
                name: "payment_method",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "booking_status",
                table: "bookings");

            migrationBuilder.DropColumn(
                name: "availability_status",
                table: "bikes");

            migrationBuilder.AddColumn<decimal>(
                name: "geofence_radius_km",
                table: "users",
                type: "decimal(65,30)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "store_address",
                table: "users",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

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

            migrationBuilder.AddColumn<string>(
                name: "store_name",
                table: "users",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "payment_method_id",
                table: "payments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "booking_status_id",
                table: "bookings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "availability_status_id",
                table: "bikes",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "availability_statuses",
                columns: table => new
                {
                    status_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    status_name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_availability_statuses", x => x.status_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "booking_statuses",
                columns: table => new
                {
                    status_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    status_name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_booking_statuses", x => x.status_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payment_methods",
                columns: table => new
                {
                    method_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    method_name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_methods", x => x.method_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "transaction_types",
                columns: table => new
                {
                    type_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    type_name = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_transaction_types", x => x.type_id);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "wallets",
                columns: table => new
                {
                    wallet_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    user_id = table.Column<int>(type: "int", nullable: false),
                    balance = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_wallets", x => x.wallet_id);
                    table.ForeignKey(
                        name: "FK_wallets_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "user_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "credit_transactions",
                columns: table => new
                {
                    transaction_id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    transaction_type_id = table.Column<int>(type: "int", nullable: false),
                    wallet_id = table.Column<int>(type: "int", nullable: false),
                    amount = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    balance_after = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    balance_before = table.Column<decimal>(type: "decimal(10,2)", precision: 10, scale: 2, nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    description = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reference_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_credit_transactions", x => x.transaction_id);
                    table.ForeignKey(
                        name: "FK_credit_transactions_transaction_types_transaction_type_id",
                        column: x => x.transaction_type_id,
                        principalTable: "transaction_types",
                        principalColumn: "type_id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_credit_transactions_wallets_wallet_id",
                        column: x => x.wallet_id,
                        principalTable: "wallets",
                        principalColumn: "wallet_id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.InsertData(
                table: "availability_statuses",
                columns: new[] { "status_id", "status_name" },
                values: new object[,]
                {
                    { 1, "Available" },
                    { 2, "Rented" },
                    { 3, "Maintenance" },
                    { 4, "Inactive" }
                });

            migrationBuilder.InsertData(
                table: "booking_statuses",
                columns: new[] { "status_id", "status_name" },
                values: new object[,]
                {
                    { 1, "Pending" },
                    { 2, "Active" },
                    { 3, "Completed" },
                    { 4, "Cancelled" }
                });

            migrationBuilder.InsertData(
                table: "payment_methods",
                columns: new[] { "method_id", "method_name" },
                values: new object[,]
                {
                    { 1, "Wallet" },
                    { 2, "GCash" },
                    { 3, "QRPH" },
                    { 4, "Cash" },
                    { 5, "PayMaya" },
                    { 6, "Credit/Debit Card" }
                });

            migrationBuilder.InsertData(
                table: "transaction_types",
                columns: new[] { "type_id", "type_name" },
                values: new object[,]
                {
                    { 1, "Load" },
                    { 2, "Withdrawal" },
                    { 3, "Rental Payment" },
                    { 4, "Rental Earnings" },
                    { 5, "Refund" },
                    { 6, "Service Fee" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_payments_payment_method_id",
                table: "payments",
                column: "payment_method_id");

            migrationBuilder.CreateIndex(
                name: "IX_bookings_booking_status_id",
                table: "bookings",
                column: "booking_status_id");

            migrationBuilder.CreateIndex(
                name: "IX_bikes_availability_status_id",
                table: "bikes",
                column: "availability_status_id");

            migrationBuilder.CreateIndex(
                name: "IX_credit_transactions_transaction_type_id",
                table: "credit_transactions",
                column: "transaction_type_id");

            migrationBuilder.CreateIndex(
                name: "IX_credit_transactions_wallet_id",
                table: "credit_transactions",
                column: "wallet_id");

            migrationBuilder.CreateIndex(
                name: "IX_wallets_user_id",
                table: "wallets",
                column: "user_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_bikes_availability_statuses_availability_status_id",
                table: "bikes",
                column: "availability_status_id",
                principalTable: "availability_statuses",
                principalColumn: "status_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_bookings_booking_statuses_booking_status_id",
                table: "bookings",
                column: "booking_status_id",
                principalTable: "booking_statuses",
                principalColumn: "status_id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_payments_payment_methods_payment_method_id",
                table: "payments",
                column: "payment_method_id",
                principalTable: "payment_methods",
                principalColumn: "method_id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
