using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace RAMZA_EBIKE_SWABI.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentIssuanceRecord : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DocumentIssuanceRecords",
                columns: table => new
                {
                    DocumentIssuanceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerInvoiceId = table.Column<int>(type: "int", nullable: false),
                    WarrantyCardIssued = table.Column<bool>(type: "bit", nullable: false),
                    VoucherCustomerIssued = table.Column<bool>(type: "bit", nullable: false),
                    VoucherCompanyIssued = table.Column<bool>(type: "bit", nullable: false),
                    IssuedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReceivedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    IssuanceDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()"),
                    Notes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentIssuanceRecords", x => x.DocumentIssuanceId);
                    table.ForeignKey(
                        name: "FK_DocumentIssuanceRecords_CustomerInvoices_CustomerInvoiceId",
                        column: x => x.CustomerInvoiceId,
                        principalTable: "CustomerInvoices",
                        principalColumn: "CustomerInvoiceId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentIssuanceRecords_CustomerInvoiceId",
                table: "DocumentIssuanceRecords",
                column: "CustomerInvoiceId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentIssuanceRecords");
        }
    }
}
