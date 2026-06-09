using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAMZA_EBIKE_SWABI.Migrations
{
    /// <inheritdoc />
    public partial class AddIsIncentiveBikeToBike : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsIncentiveBike",
                table: "VendorBillItem",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsIncentiveBike",
                table: "Bikes",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsIncentiveBike",
                table: "VendorBillItem");

            migrationBuilder.DropColumn(
                name: "IsIncentiveBike",
                table: "Bikes");
        }
    }
}
