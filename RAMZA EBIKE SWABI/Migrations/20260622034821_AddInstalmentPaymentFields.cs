using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAMZA_EBIKE_SWABI.Migrations
{
    /// <inheritdoc />
    public partial class AddInstalmentPaymentFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PaidAccount",
                table: "InvoiceInstalments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidCash",
                table: "InvoiceInstalments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "PaymentSource",
                table: "InvoiceInstalments",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PaidAccount",
                table: "InvoiceInstalments");

            migrationBuilder.DropColumn(
                name: "PaidCash",
                table: "InvoiceInstalments");

            migrationBuilder.DropColumn(
                name: "PaymentSource",
                table: "InvoiceInstalments");
        }
    }
}
