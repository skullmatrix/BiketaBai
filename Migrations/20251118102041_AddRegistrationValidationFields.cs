using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BiketaBai.Migrations
{
    /// <inheritdoc />
    public partial class AddRegistrationValidationFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "address_verified",
                table: "users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "address_verified_at",
                table: "users",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "id_extracted_address",
                table: "users",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "id_verified",
                table: "users",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "id_verified_at",
                table: "users",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "store_address",
                table: "users",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "store_name",
                table: "users",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "address_verified",
                table: "users");

            migrationBuilder.DropColumn(
                name: "address_verified_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "id_extracted_address",
                table: "users");

            migrationBuilder.DropColumn(
                name: "id_verified",
                table: "users");

            migrationBuilder.DropColumn(
                name: "id_verified_at",
                table: "users");

            migrationBuilder.DropColumn(
                name: "store_address",
                table: "users");

            migrationBuilder.DropColumn(
                name: "store_name",
                table: "users");
        }
    }
}
