using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAMZA_EBIKE_SWABI.Migrations
{
    /// <inheritdoc />
    public partial class AddAmountPaidCashAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AmountPaidAccount",
                table: "CustomerInvoices",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountPaidCash",
                table: "CustomerInvoices",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AmountPaidAccount",
                table: "CustomerInvoices");

            migrationBuilder.DropColumn(
                name: "AmountPaidCash",
                table: "CustomerInvoices");
        }
    }
}
