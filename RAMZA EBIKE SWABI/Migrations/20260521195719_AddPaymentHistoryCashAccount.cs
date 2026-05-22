using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAMZA_EBIKE_SWABI.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentHistoryCashAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AccountTransactionId",
                table: "CustomerPaymentHistories",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountPaidAccount",
                table: "CustomerPaymentHistories",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountPaidCash",
                table: "CustomerPaymentHistories",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "CashTransactionId",
                table: "CustomerPaymentHistories",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccountTransactionId",
                table: "CustomerPaymentHistories");

            migrationBuilder.DropColumn(
                name: "AmountPaidAccount",
                table: "CustomerPaymentHistories");

            migrationBuilder.DropColumn(
                name: "AmountPaidCash",
                table: "CustomerPaymentHistories");

            migrationBuilder.DropColumn(
                name: "CashTransactionId",
                table: "CustomerPaymentHistories");
        }
    }
}
