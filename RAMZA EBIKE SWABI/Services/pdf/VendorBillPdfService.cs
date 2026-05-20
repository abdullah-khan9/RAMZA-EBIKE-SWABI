using Microsoft.Win32;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Ramza_EBike_Swabi.Models;
using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace Ramza_EBike_Swabi.Services.Pdf
{
    public static class VendorBillPdfService
    {
        private const string ShopName = "Swabi Enterprises";
        private const string ShopNTN = "NTN: I039290";
        private const string ShopAddress = "Main Jahangira Road Near Sadaat CNG, Swabi";
        private const string ShopPhone = "+92 345 9996397";

        public static void Generate(VendorBill bill)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            // ── Ask user where to save ──────────────────────────────
            string filePath = string.Empty;

            Application.Current.Dispatcher.Invoke(() =>
            {
                var dlg = new SaveFileDialog
                {
                    Title = "Save Vendor Bill PDF",
                    Filter = "PDF Files (*.pdf)|*.pdf",
                    DefaultExt = "pdf",
                    FileName = $"Bill_{bill.Id}_{bill.Vendor?.VendorName ?? "Vendor"}_{DateTime.Now:yyyyMMdd_HHmmss}"
                };
                if (dlg.ShowDialog() == true)
                    filePath = dlg.FileName;
            });

            if (string.IsNullOrEmpty(filePath)) return;   // user cancelled

            // ── Computed values ─────────────────────────────────────
            string logoPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory, "Resources", "logo.png");

            decimal netBill = bill.PreviousBalance + bill.TotalAmount;

            string billStatus = bill.RemainingBalance <= 0 ? "CLEAR"
                : bill.AmountPaid > 0 ? "PARTIALLY PAID"
                : "UNPAID";

            string paidVia = bill.PaymentSource switch
            {
                "Cash" => "Cash Balance",
                "Account" => "Account / Bank",
                "VendorCash" => "Vendor Cash",
                "None" => "Not Specified",
                _ => bill.PaymentSource ?? "—"
            };

            // ── Generate PDF ────────────────────────────────────────
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(18);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                    // ══════════════════════════════════════════
                    // HEADER
                    // ══════════════════════════════════════════
                    page.Header().Column(headerCol =>
                    {
                        headerCol.Item().Row(row =>
                        {
                            // Logo
                            row.ConstantItem(70).AlignMiddle().Element(e =>
                            {
                                if (File.Exists(logoPath))
                                    e.Image(logoPath, ImageScaling.FitArea);
                            });

                            // Centre: company info
                            row.RelativeItem().AlignCenter().AlignMiddle().Column(col =>
                            {
                                col.Item().AlignCenter()
                                    .Text(ShopName).FontSize(18).Bold()
                                    .FontColor(Color.FromHex("1B4079"));
                                col.Item().AlignCenter().Text(ShopNTN);
                                col.Item().AlignCenter().Text(ShopAddress);
                                col.Item().AlignCenter().Text($"Phone: {ShopPhone}");
                            });

                            // Right: bill meta
                            row.ConstantItem(190).AlignMiddle().Column(col =>
                            {
                                col.Item().AlignRight()
                                    .Text("VENDOR PURCHASE BILL")
                                    .FontSize(13).Bold()
                                    .FontColor(Color.FromHex("1B4079"));

                                col.Item().AlignRight().Text($"Bill #: {bill.Id}");
                                col.Item().AlignRight()
                                    .Text($"Date: {bill.BillDate:dd-MMM-yyyy}");
                                col.Item().AlignRight()
                                    .Text($"Doc #: {bill.Items?.FirstOrDefault()?.DocumentNumber ?? "-"}");
                                col.Item().AlignRight()
                                    .Text($"Paid via: {paidVia}").Bold();

                                var statusColor = billStatus == "CLEAR"
                                    ? Color.FromHex("1B8A3D")
                                    : billStatus == "PARTIALLY PAID"
                                        ? Color.FromHex("E67E00")
                                        : Color.FromHex("C0392B");

                                col.Item().AlignRight()
                                    .Text($"Status: {billStatus}")
                                    .Bold().FontColor(statusColor);
                            });
                        });

                        headerCol.Item().PaddingTop(4)
                            .LineHorizontal(1.5f)
                            .LineColor(Color.FromHex("1B4079"));
                    });

                    // ══════════════════════════════════════════
                    // CONTENT
                    // ══════════════════════════════════════════
                    page.Content().PaddingTop(8).Column(col =>
                    {
                        // ── Vendor info + Financial summary boxes ─────
                        col.Item().Row(row =>
                        {
                            // Left: Vendor details
                            row.RelativeItem()
                                .Border(0.5f).BorderColor(Color.FromHex("CCCCCC"))
                                .Background(Color.FromHex("F0F4FA"))
                                .Padding(8).Column(c =>
                                {
                                    c.Item().Text("VENDOR DETAILS")
                                        .FontSize(8).Bold()
                                        .FontColor(Color.FromHex("1B4079"));

                                    c.Item().PaddingTop(4).Table(t =>
                                    {
                                        t.ColumnsDefinition(cd =>
                                        {
                                            cd.ConstantColumn(100);
                                            cd.RelativeColumn();
                                        });

                                        void VR(string lbl, string val)
                                        {
                                            t.Cell().Text(lbl).Bold();
                                            t.Cell().Text(val ?? "-");
                                        }

                                        VR("Vendor Name:", bill.Vendor?.VendorName ?? "-");
                                        VR("Vendor ID:", bill.VendorId.ToString());
                                        VR("Bill Date:", bill.BillDate.ToString("dd-MMM-yyyy"));
                                        VR("Document #:", bill.Items?.FirstOrDefault()?.DocumentNumber ?? "-");
                                    });
                                });

                            row.ConstantItem(8);

                            // Right: Full financial summary — ALL 6 figures
                            row.ConstantItem(280)
                                .Border(0.5f).BorderColor(Color.FromHex("CCCCCC"))
                                .Background(Color.FromHex("F0F4FA"))
                                .Padding(8).Column(c =>
                                {
                                    c.Item().Text("FINANCIAL SUMMARY")
                                        .FontSize(8).Bold()
                                        .FontColor(Color.FromHex("1B4079"));

                                    c.Item().PaddingTop(4).Table(t =>
                                    {
                                        t.ColumnsDefinition(cd =>
                                        {
                                            cd.ConstantColumn(150);
                                            cd.RelativeColumn();
                                        });

                                        void FR(string lbl, string val,
                                            bool bold = false, string? hex = null)
                                        {
                                            t.Cell().Text(lbl).Bold();
                                            var txt = t.Cell().Text(val);
                                            if (bold) txt.Bold();
                                            if (hex != null) txt.FontColor(Color.FromHex(hex));
                                        }

                                        FR("Previous Balance:",
                                            $"PKR {bill.PreviousBalance:N2}",
                                            false, "777777");

                                        FR("Total Amount (bill):",
                                            $"PKR {bill.TotalAmount:N2}");

                                        FR("Net Bill (Prev + Total):",
                                            $"PKR {netBill:N2}",
                                            true, "1B4079");

                                        FR("Amount Paid:",
                                            $"PKR {bill.AmountPaid:N2}",
                                            true, "1B8A3D");

                                        FR("Paid Via:", paidVia);

                                        FR("Remaining Balance:",
                                            $"PKR {bill.RemainingBalance:N2}",
                                            true,
                                            bill.RemainingBalance <= 0 ? "1B8A3D" : "C0392B");
                                    });
                                });
                        });

                        col.Item().PaddingTop(8);

                        // ── Items Table ───────────────────────────────
                        col.Item().Text("BILL ITEMS")
                            .FontSize(8).Bold()
                            .FontColor(Color.FromHex("1B4079"));

                        col.Item().PaddingTop(3)
                            .Border(0.5f).BorderColor(Color.FromHex("CCCCCC"))
                            .Table(t =>
                            {
                                t.ColumnsDefinition(c =>
                                {
                                    c.ConstantColumn(22);   // #
                                    c.RelativeColumn(1.3f); // Model
                                    c.RelativeColumn(1.2f); // Motor No
                                    c.RelativeColumn(1.2f); // Chassis No
                                    c.RelativeColumn(0.6f); // Qty
                                    c.RelativeColumn(1f);   // Rate
                                    c.RelativeColumn(0.7f); // Comm%
                                    c.RelativeColumn(0.7f); // Tax%
                                    c.RelativeColumn(0.7f); // Extra%
                                    c.RelativeColumn(1.1f); // Commission
                                    c.RelativeColumn(1.1f); // Tax
                                    c.RelativeColumn(1.1f); // Total Price
                                });

                                t.Header(h =>
                                {
                                    void H(string txt) =>
                                        h.Cell()
                                            .Background(Color.FromHex("1B4079"))
                                            .Padding(4)
                                            .Text(txt).FontSize(8).Bold()
                                            .FontColor(Colors.White);

                                    H("#"); H("Model"); H("Motor No"); H("Chassis No");
                                    H("Qty"); H("Rate"); H("Comm%"); H("Tax%"); H("Extra%");
                                    H("Commission"); H("Tax"); H("Total Price");
                                });

                                int i = 1;
                                bool alt = false;

                                foreach (var item in bill.Items ?? Enumerable.Empty<VendorBillItem>())
                                {
                                    var bg = alt ? Color.FromHex("F5F8FF") : Colors.White;
                                    alt = !alt;

                                    void Cell(string val) =>
                                        t.Cell().Background(bg).Padding(3).Text(val ?? "-");

                                    Cell(i++.ToString());
                                    Cell(item.Model);
                                    Cell(item.MotorNumber);
                                    Cell(item.ChassisNumber);
                                    Cell(item.Qty.ToString());
                                    Cell(item.BillRate.ToString("N2"));
                                    Cell(item.CommissionPercent.ToString("N1"));
                                    Cell(item.TaxOnCommissionPercent.ToString("N1"));
                                    Cell(item.ExtraDiscountPercent.ToString("N1"));
                                    Cell(item.CommissionAmount.ToString("N2"));
                                    Cell(item.TaxOnCommissionAmount.ToString("N2"));
                                    Cell(item.TotalWholesalePrice.ToString("N2"));
                                }
                            });

                        // ── Total bar ─────────────────────────────────
                        col.Item()
                            .Background(Color.FromHex("1B4079"))
                            .Padding(5).AlignRight()
                            .Text($"TOTAL AMOUNT:  PKR {bill.TotalAmount:N2}")
                            .Bold().FontColor(Colors.White);

                        // ── Commission summary + Final balance row ────
                        col.Item().PaddingTop(8).Row(row =>
                        {
                            // Left: commission + tax
                            row.RelativeItem()
                                .Border(0.5f).BorderColor(Color.FromHex("CCCCCC"))
                                .Background(Color.FromHex("F0F4FA"))
                                .Padding(8).Column(c =>
                                {
                                    c.Item().Text("COMMISSION SUMMARY")
                                        .FontSize(8).Bold()
                                        .FontColor(Color.FromHex("1B4079"));

                                    c.Item().PaddingTop(4).Table(t =>
                                    {
                                        t.ColumnsDefinition(cd =>
                                        {
                                            cd.ConstantColumn(130);
                                            cd.RelativeColumn();
                                        });

                                        void CR(string lbl, string val)
                                        {
                                            t.Cell().Text(lbl).Bold();
                                            t.Cell().Text(val);
                                        }

                                        CR("Total Commission:", $"PKR {bill.TotalCommission:N2}");
                                        CR("Total Tax Paid:", $"PKR {bill.TotalTaxPaid:N2}");
                                    });
                                });

                            row.ConstantItem(8);

                            // Right: complete balance breakdown (repeat for emphasis at bottom)
                            row.ConstantItem(295)
                                .Border(0.5f).BorderColor(Color.FromHex("CCCCCC"))
                                .Background(Color.FromHex("F0F4FA"))
                                .Padding(8).Column(c =>
                                {
                                    c.Item().Text("FINAL BALANCE SUMMARY")
                                        .FontSize(8).Bold()
                                        .FontColor(Color.FromHex("1B4079"));

                                    c.Item().PaddingTop(4).Table(t =>
                                    {
                                        t.ColumnsDefinition(cd =>
                                        {
                                            cd.ConstantColumn(155);
                                            cd.RelativeColumn();
                                        });

                                        void BR(string lbl, string val,
                                            bool bold = false, string? hex = null)
                                        {
                                            t.Cell().Text(lbl).Bold();
                                            var txt = t.Cell().Text(val);
                                            if (bold) txt.Bold();
                                            if (hex != null) txt.FontColor(Color.FromHex(hex));
                                        }

                                        BR("Previous Balance:",
                                            $"PKR {bill.PreviousBalance:N2}",
                                            false, "777777");

                                        BR("Total Amount (bill):",
                                            $"PKR {bill.TotalAmount:N2}");

                                        BR("Net Bill (Prev + Total):",
                                            $"PKR {netBill:N2}",
                                            true, "1B4079");

                                        BR("Amount Paid:",
                                            $"PKR {bill.AmountPaid:N2}",
                                            true, "1B8A3D");

                                        BR("Paid Via:", paidVia);

                                        BR("Remaining Balance:",
                                            $"PKR {bill.RemainingBalance:N2}",
                                            true,
                                            bill.RemainingBalance <= 0 ? "1B8A3D" : "C0392B");
                                    });
                                });
                        });

                        // ── Remarks ───────────────────────────────────
                        if (!string.IsNullOrWhiteSpace(bill.Remarks))
                        {
                            col.Item().PaddingTop(8)
                                .Border(0.5f).BorderColor(Color.FromHex("F9A825"))
                                .Background(Color.FromHex("FFFDE7"))
                                .Padding(8).Column(c =>
                                {
                                    c.Item().Text("REMARKS")
                                        .FontSize(8).Bold()
                                        .FontColor(Color.FromHex("E65100"));
                                    c.Item().PaddingTop(3).Text(bill.Remarks);
                                });
                        }

                        col.Item().PaddingTop(18);

                        // ── Signatures ────────────────────────────────
                        col.Item().Row(row =>
                        {
                            void Sig(RowDescriptor r, string label, string sub = "")
                            {
                                r.RelativeItem().AlignCenter().Column(c =>
                                {
                                    c.Item().AlignCenter()
                                        .Text("________________________________");
                                    c.Item().AlignCenter().Text(label);
                                    if (!string.IsNullOrEmpty(sub))
                                        c.Item().AlignCenter().Text(sub).FontSize(8);
                                });
                            }

                            Sig(row, "Vendor Signature");
                            Sig(row, "Authorized Signature", ShopName);
                            Sig(row, "Received By");
                        });
                    });

                    // ══════════════════════════════════════════
                    // FOOTER
                    // ══════════════════════════════════════════
                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("This is a computer-generated document.  ").FontSize(8);
                        t.Span(ShopName).Bold().FontSize(8);
                        t.Span($"  |  {ShopPhone}  |  {ShopNTN}").FontSize(8);
                    });
                });
            }).GeneratePdf(filePath);

            // Open the saved PDF
            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }
    }
}