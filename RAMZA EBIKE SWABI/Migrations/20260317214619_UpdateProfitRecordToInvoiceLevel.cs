using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAMZA_EBIKE_SWABI.Migrations
{
    /// <inheritdoc />
    public partial class UpdateProfitRecordToInvoiceLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ChassisNumber",
                table: "ProfitRecords");

            migrationBuilder.DropColumn(
                name: "Model",
                table: "ProfitRecords");

            migrationBuilder.RenameColumn(
                name: "WholesalePrice",
                table: "ProfitRecords",
                newName: "TotalWholesaleCost");

            migrationBuilder.RenameColumn(
                name: "SalePrice",
                table: "ProfitRecords",
                newName: "TotalSalePrice");

            migrationBuilder.RenameColumn(
                name: "MotorNumber",
                table: "ProfitRecords",
                newName: "BikeModels");

            migrationBuilder.AddColumn<int>(
                name: "BikeCount",
                table: "ProfitRecords",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "Discount",
                table: "ProfitRecords",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BikeCount",
                table: "ProfitRecords");

            migrationBuilder.DropColumn(
                name: "Discount",
                table: "ProfitRecords");

            migrationBuilder.RenameColumn(
                name: "TotalWholesaleCost",
                table: "ProfitRecords",
                newName: "WholesalePrice");

            migrationBuilder.RenameColumn(
                name: "TotalSalePrice",
                table: "ProfitRecords",
                newName: "SalePrice");

            migrationBuilder.RenameColumn(
                name: "BikeModels",
                table: "ProfitRecords",
                newName: "MotorNumber");

            migrationBuilder.AddColumn<string>(
                name: "ChassisNumber",
                table: "ProfitRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Model",
                table: "ProfitRecords",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }
    }
}
