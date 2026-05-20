using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAMZA_EBIKE_SWABI.Migrations
{
    /// <inheritdoc />
    public partial class AddBikeFieldsToVendorBillItem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BatteryCapacity",
                table: "VendorBillItem",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Brand",
                table: "VendorBillItem",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ChassisNumber",
                table: "VendorBillItem",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Color",
                table: "VendorBillItem",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MotorNumber",
                table: "VendorBillItem",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "MotorPower",
                table: "VendorBillItem",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Warranty",
                table: "VendorBillItem",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BatteryCapacity",
                table: "VendorBillItem");

            migrationBuilder.DropColumn(
                name: "Brand",
                table: "VendorBillItem");

            migrationBuilder.DropColumn(
                name: "ChassisNumber",
                table: "VendorBillItem");

            migrationBuilder.DropColumn(
                name: "Color",
                table: "VendorBillItem");

            migrationBuilder.DropColumn(
                name: "MotorNumber",
                table: "VendorBillItem");

            migrationBuilder.DropColumn(
                name: "MotorPower",
                table: "VendorBillItem");

            migrationBuilder.DropColumn(
                name: "Warranty",
                table: "VendorBillItem");
        }
    }
}
