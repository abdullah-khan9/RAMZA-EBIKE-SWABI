using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAMZA_EBIKE_SWABI.Migrations
{
    /// <inheritdoc />
    public partial class AddFlatPricingSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "FlatPurchasePrice",
                table: "VendorBillItem",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsFlatPrice",
                table: "VendorBillItem",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "RetailSalePrice",
                table: "VendorBillItem",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FlatPurchasePrice",
                table: "VendorBillItem");

            migrationBuilder.DropColumn(
                name: "IsFlatPrice",
                table: "VendorBillItem");

            migrationBuilder.DropColumn(
                name: "RetailSalePrice",
                table: "VendorBillItem");
        }
    }
}
