using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BiketaBai.Migrations
{
    /// <inheritdoc />
    public partial class AddIdDocumentBackUrl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "id_document_back_url",
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
                name: "id_document_back_url",
                table: "users");
        }
    }
}
