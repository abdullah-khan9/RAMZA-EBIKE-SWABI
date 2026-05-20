using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAMZA_EBIKE_SWABI.Migrations
{
    /// <inheritdoc />
    public partial class AddVendorIncentivesTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VendorIncentives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    VendorId = table.Column<int>(type: "int", nullable: false),
                    VendorName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IncentiveName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Destination = table.Column<int>(type: "int", nullable: false),
                    IncentiveDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    Remarks = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    VendorCashLedgerId = table.Column<int>(type: "int", nullable: true),
                    AccountTransactionId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VendorIncentives", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VendorIncentives");
        }
    }
}
