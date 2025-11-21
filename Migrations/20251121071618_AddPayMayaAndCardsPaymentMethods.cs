using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace BiketaBai.Migrations
{
    /// <inheritdoc />
    public partial class AddPayMayaAndCardsPaymentMethods : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "payment_methods",
                columns: new[] { "method_id", "method_name" },
                values: new object[,]
                {
                    { 5, "PayMaya" },
                    { 6, "Credit/Debit Card" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "payment_methods",
                keyColumn: "method_id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "payment_methods",
                keyColumn: "method_id",
                keyValue: 6);
        }
    }
}
