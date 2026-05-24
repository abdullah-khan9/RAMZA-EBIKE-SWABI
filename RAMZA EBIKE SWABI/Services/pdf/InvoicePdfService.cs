using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Ramza_EBike_Swabi.Models;
using Ramza_EBike_Swabi.Data;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Ramza_EBike_Swabi.Services.Pdf
{
    public static class InvoicePdfService
    {
        private const string ShopName = "Swabi Enterprises";
        private const string ShopNTN = "NTN: I039290";
        private const string ShopAddress = "Main Jahangira Road Near Sadaat CNG, Swabi";
        private const string ShopPhone = "+92 345 9996397";

        /// <summary>
        /// Generate PDF and open it automatically
        /// </summary>
        public static void Generate(CustomerInvoice invoice)
        {
            GenerateInternal(invoice, openAfterGenerate: true);
        }

        /// <summary>
        /// ✅ NEW: Regenerate PDF silently without opening it
        /// </summary>
        public static void RegenerateWithoutOpening(CustomerInvoice invoice)
        {
            GenerateInternal(invoice, openAfterGenerate: false);
        }

        /// <summary>
        /// Internal method that handles PDF generation
        /// </summary>
        private static void GenerateInternal(CustomerInvoice invoice, bool openAfterGenerate)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            // ✅ Fetch payment history from DB
            List<CustomerPaymentHistory> paymentHistory;
            using (var db = new AppDbContext())
            {
                paymentHistory = db.CustomerPaymentHistories
                    .Where(h => h.CustomerInvoiceId == invoice.CustomerInvoiceId)
                    .OrderBy(h => h.PaymentDate)
                    .ThenBy(h => h.Id)
                    .ToList();
            }

            string logoPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Resources", "logo.png");

            string filePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"Invoice_{invoice.Customer?.Name ?? "Customer"}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

            string billStatus = invoice.RemainingBalance <= 0 ? "CLEAR"
                              : invoice.AmountPaid > 0 ? "PARTIALLY PAID"
                                                               : "UNPAID";

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(18);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                    // ═══════════════ HEADER ═══════════════
                    page.Header().Column(headerCol =>
                    {
                        headerCol.Item().Row(row =>
                        {
                            row.ConstantItem(70).AlignMiddle().Element(e =>
                            {
                                if (File.Exists(logoPath))
                                    e.Image(logoPath, ImageScaling.FitArea);
                                else
                                    e.Text(string.Empty);
                            });

                            row.RelativeItem().AlignCenter().AlignMiddle().Column(col =>
                            {
                                col.Item().AlignCenter().Text(ShopName)
                                   .FontSize(18).Bold().FontColor(Color.FromHex("1B4079"));
                                col.Item().AlignCenter().Text(ShopNTN)
                                   .FontSize(9).FontColor(Color.FromHex("444444"));
                                col.Item().AlignCenter().Text(ShopAddress)
                                   .FontSize(9).FontColor(Color.FromHex("444444"));
                                col.Item().AlignCenter().Text($"Phone: {ShopPhone}")
                                   .FontSize(9).FontColor(Color.FromHex("444444"));
                            });

                            row.ConstantItem(185).AlignMiddle().Column(col =>
                            {
                                col.Item().AlignRight().Text("CUSTOMER INVOICE")
                                   .FontSize(13).Bold().FontColor(Color.FromHex("1B4079"));
                                col.Item().PaddingTop(3).AlignRight()
                                   .Text($"Invoice #: INV-{invoice.CustomerInvoiceId:D4}").FontSize(9);
                                col.Item().AlignRight()
                                   .Text($"Date: {invoice.InvoiceDate:dd-MMM-yyyy}").FontSize(9);
                                col.Item().AlignRight()
                                   .Text($"Payment: {invoice.PaymentMethod ?? "Cash"}")
                                   .FontSize(9).Bold();

                                var statusColor = billStatus == "CLEAR" ? Color.FromHex("1B8A3D")
                                                : billStatus == "PARTIALLY PAID" ? Color.FromHex("E67E00")
                                                                                 : Color.FromHex("C0392B");
                                col.Item().PaddingTop(3).AlignRight()
                                   .Text($"Status: {billStatus}")
                                   .FontSize(9).Bold().FontColor(statusColor);
                            });
                        });

                        headerCol.Item().PaddingTop(4)
                                 .LineHorizontal(1.5f)
                                 .LineColor(Color.FromHex("1B4079"));
                    });

                    // ═══════════════ CONTENT ═══════════════
                    page.Content().PaddingTop(8).Column(col =>
                    {
                        // Customer + Payment info row
                        col.Item().Row(row =>
                        {
                            // Customer details
                            row.RelativeItem().Border(0.5f)
                               .BorderColor(Color.FromHex("CCCCCC"))
                               .Background(Color.FromHex("F0F4FA"))
                               .Padding(8).Column(c =>
                               {
                                   c.Item().Text("CUSTOMER DETAILS")
                                    .FontSize(8).Bold().FontColor(Color.FromHex("1B4079"));

                                   c.Item().PaddingTop(4).Table(t =>
                                   {
                                       t.ColumnsDefinition(cd =>
                                       {
                                           cd.ConstantColumn(80);
                                           cd.RelativeColumn();
                                       });

                                       void R(string lbl, string val)
                                       {
                                           t.Cell().PaddingBottom(2).Text(lbl).Bold().FontSize(9);
                                           t.Cell().PaddingBottom(2).Text(val ?? "-").FontSize(9);
                                       }

                                       R("Name:", invoice.Customer?.Name ?? "-");
                                       R("Father Name:", invoice.Customer?.FatherName ?? "-");
                                       R("Contact:", invoice.Customer?.Contact ?? "-");
                                       R("CNIC:", invoice.Customer?.CNIC ?? "-");
                                       R("Address:", invoice.Customer?.Address ?? "-");
                                   });
                               });

                            row.ConstantItem(8);

                            // Payment details
                            row.ConstantItem(230).Border(0.5f)
                               .BorderColor(Color.FromHex("CCCCCC"))
                               .Background(Color.FromHex("F0F4FA"))
                               .Padding(8).Column(c =>
                               {
                                   c.Item().Text("PAYMENT DETAILS")
                                    .FontSize(8).Bold().FontColor(Color.FromHex("1B4079"));

                                   c.Item().PaddingTop(4).Table(t =>
                                   {
                                       t.ColumnsDefinition(cd =>
                                       {
                                           cd.ConstantColumn(110);
                                           cd.RelativeColumn();
                                       });

                                       void R(string lbl, string val,
                                           bool bold = false, string? hex = null)
                                       {
                                           t.Cell().PaddingBottom(2).Text(lbl).Bold().FontSize(9);
                                           var txt = t.Cell().PaddingBottom(2).Text(val).FontSize(9);
                                           if (bold) txt.Bold();
                                           if (hex != null) txt.FontColor(Color.FromHex(hex));
                                       }

                                       R("Payment Method:", invoice.PaymentMethod ?? "Cash");
                                       R("Total Amount:", $"PKR {invoice.TotalAmount:N2}");
                                       R("Discount:", $"PKR {invoice.Discount:N2}");
                                       R("Net Bill:", $"PKR {invoice.NetBill:N2}", bold: true);

                                       // Show Cash + Account paid at invoice creation
                                       if (invoice.AmountPaidCash > 0 || invoice.AmountPaidAccount > 0)
                                       {
                                           if (invoice.AmountPaidCash > 0)
                                               R("Paid (Cash):", $"PKR {invoice.AmountPaidCash:N2}",
                                                   hex: "1B8A3D");
                                           if (invoice.AmountPaidAccount > 0)
                                               R("Paid (Account):", $"PKR {invoice.AmountPaidAccount:N2}",
                                                   hex: "1565C0");
                                       }
                                       else if (invoice.AmountPaid > 0)
                                       {
                                           R("Amount Paid:", $"PKR {invoice.AmountPaid:N2}");
                                       }

                                       R("Remaining:", $"PKR {invoice.RemainingBalance:N2}",
                                           bold: invoice.RemainingBalance > 0,
                                           hex: invoice.RemainingBalance > 0 ? "C0392B" : "1B8A3D");

                                       R("Bill Status:", billStatus, bold: true,
                                           hex: billStatus == "CLEAR" ? "1B8A3D"
                                              : billStatus == "PARTIALLY PAID" ? "E67E00"
                                                                               : "C0392B");

                                       if (invoice.DueDate.HasValue && invoice.RemainingBalance > 0)
                                       {
                                           bool isOverdue = invoice.DueDate.Value.Date < DateTime.Today;
                                           bool isDueToday = invoice.DueDate.Value.Date == DateTime.Today;
                                           bool isDueSoon = invoice.DueDate.Value.Date <= DateTime.Today.AddDays(3);

                                           string dueDateHex = (isOverdue || isDueToday) ? "C0392B"
                                                               : isDueSoon ? "E67E00"
                                                                                           : "1B4079";
                                           string dueDateLabel = isOverdue ? "Due Date (OVERDUE):"
                                                               : isDueToday ? "Due Date (TODAY):"
                                                                            : "Due Date:";

                                           R(dueDateLabel,
                                             invoice.DueDate.Value.ToString("dd-MMM-yyyy"),
                                             bold: true, hex: dueDateHex);
                                       }
                                   });
                               });
                        });

                        col.Item().PaddingTop(8);

                        // Bike items
                        col.Item().Text("BIKE DETAILS")
                           .FontSize(8).Bold().FontColor(Color.FromHex("1B4079"));

                        col.Item().PaddingTop(3).Border(0.5f)
                           .BorderColor(Color.FromHex("CCCCCC"))
                           .Table(t =>
                           {
                               t.ColumnsDefinition(c =>
                               {
                                   c.ConstantColumn(22);
                                   c.RelativeColumn(2.2f);
                                   c.RelativeColumn(1.5f);
                                   c.RelativeColumn(1.5f);
                                   c.RelativeColumn(1f);
                                   c.RelativeColumn(1.8f);
                                   c.RelativeColumn(2f);
                                   c.RelativeColumn(1.2f);
                                   c.ConstantColumn(28);
                                   c.RelativeColumn(1.5f);
                                   c.RelativeColumn(1.5f);
                               });

                               t.Header(h =>
                               {
                                   void H(string txt) =>
                                       h.Cell().Background(Color.FromHex("1B4079"))
                                        .Padding(5).Text(txt)
                                        .FontSize(8).Bold().FontColor(Colors.White);

                                   H("#"); H("Model"); H("Brand"); H("Motor Power");
                                   H("Color"); H("Motor No."); H("Chassis No.");
                                   H("Warranty"); H("Qty"); H("Price (PKR)"); H("Total (PKR)");
                               });

                               int idx = 1;
                               bool alt = false;
                               foreach (var item in (invoice.Items
                                   ?? Enumerable.Empty<CustomerInvoiceItem>())
                                   .Where(i => !string.IsNullOrWhiteSpace(i.MotorNumber)))
                               {
                                   var bg = alt ? Color.FromHex("F5F8FF") : Colors.White;
                                   alt = !alt;

                                   void Cell(string val) =>
                                       t.Cell().Background(bg)
                                        .BorderBottom(0.3f).BorderColor(Color.FromHex("DDDDDD"))
                                        .Padding(4).Text(val ?? "-").FontSize(9);

                                   Cell(idx++.ToString());
                                   Cell(item.Model);
                                   Cell(item.Brand);
                                   Cell(item.MotorPower);
                                   Cell(item.Color);
                                   Cell(item.MotorNumber);
                                   Cell(item.ChassisNumber);
                                   Cell(item.Warranty);
                                   Cell(item.Quantity.ToString());
                                   Cell(item.Price.ToString("N2"));
                                   Cell(item.TotalPrice.ToString("N2"));
                               }
                           });

                        col.Item().Background(Color.FromHex("1B4079"))
                           .Padding(5).AlignRight()
                           .Text($"TOTAL AMOUNT:  PKR {invoice.TotalAmount:N2}")
                           .FontSize(10).Bold().FontColor(Colors.White);

                        // ✅ Payment History section
                        if (paymentHistory.Count > 0)
                        {
                            col.Item().PaddingTop(8);
                            col.Item().Text("PAYMENT HISTORY")
                               .FontSize(8).Bold().FontColor(Color.FromHex("1B4079"));

                            col.Item().PaddingTop(3).Border(0.5f)
                               .BorderColor(Color.FromHex("CCCCCC"))
                               .Table(t =>
                               {
                                   t.ColumnsDefinition(c =>
                                   {
                                       c.ConstantColumn(22);  // #
                                       c.RelativeColumn(2f);  // Date
                                       c.RelativeColumn(2f);  // Cash
                                       c.RelativeColumn(2f);  // Account
                                       c.RelativeColumn(2f);  // Total
                                       c.RelativeColumn(2f);  // Remaining
                                       c.RelativeColumn(2f);  // Method
                                       c.RelativeColumn(2f);  // Received By
                                   });

                                   t.Header(h =>
                                   {
                                       void H(string txt) =>
                                           h.Cell().Background(Color.FromHex("2E7D32"))
                                            .Padding(5).Text(txt)
                                            .FontSize(8).Bold().FontColor(Colors.White);

                                       H("#"); H("Date"); H("Cash (PKR)");
                                       H("Account (PKR)"); H("Total (PKR)");
                                       H("Remaining (PKR)"); H("Method"); H("Received By");
                                   });

                                   int i = 1;
                                   bool alt = false;
                                   foreach (var ph in paymentHistory)
                                   {
                                       var bg = alt ? Color.FromHex("F0FFF4") : Colors.White;
                                       alt = !alt;

                                       void PC(string val, bool bold = false, string? hex = null)
                                       {
                                           var txt = t.Cell().Background(bg)
                                               .BorderBottom(0.3f).BorderColor(Color.FromHex("DDDDDD"))
                                               .Padding(4).Text(val ?? "-").FontSize(8.5f);
                                           if (bold) txt.Bold();
                                           if (hex != null) txt.FontColor(Color.FromHex(hex));
                                       }

                                       PC(i++.ToString());
                                       PC(ph.PaymentDate.ToString("dd-MMM-yyyy"));
                                       PC(ph.AmountPaidCash > 0 ? ph.AmountPaidCash.ToString("N0") : "-", hex: "1B8A3D");
                                       PC(ph.AmountPaidAccount > 0 ? ph.AmountPaidAccount.ToString("N0") : "-", hex: "1565C0");
                                       PC(ph.AmountPaid.ToString("N0"), bold: true);
                                       PC(ph.RemainingAfter.ToString("N0"),
                                           bold: ph.RemainingAfter > 0,
                                           hex: ph.RemainingAfter > 0 ? "C0392B" : "1B8A3D");
                                       PC(ph.PaymentMethod);
                                       PC(ph.ReceivedBy);
                                   }
                               });
                        }

                        col.Item().PaddingTop(8);

                        // Remarks + Checklist
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Border(0.5f)
                               .BorderColor(Color.FromHex("CCCCCC"))
                               .Padding(8).Column(c =>
                               {
                                   c.Item().Text("REMARKS")
                                    .FontSize(8).Bold().FontColor(Color.FromHex("1B4079"));
                                   c.Item().PaddingTop(3)
                                    .Text(string.IsNullOrWhiteSpace(invoice.Remarks)
                                        ? "-" : invoice.Remarks).FontSize(9);
                               });

                            row.ConstantItem(8);

                            row.ConstantItem(230).Border(0.5f)
                               .BorderColor(Color.FromHex("CCCCCC"))
                               .Padding(8).Column(c =>
                               {
                                   c.Item().Text("CHECKLIST")
                                    .FontSize(8).Bold().FontColor(Color.FromHex("1B4079"));

                                   void Check(string label, bool ticked)
                                   {
                                       c.Item().PaddingTop(3).Row(r =>
                                       {
                                           r.ConstantItem(14).Border(0.7f)
                                            .BorderColor(Color.FromHex("333333"))
                                            .AlignCenter().AlignMiddle()
                                            .Text(ticked ? "✓" : " ")
                                            .FontSize(8).Bold()
                                            .FontColor(Color.FromHex("1B4079"));
                                           r.RelativeItem().PaddingLeft(4)
                                            .Text(label).FontSize(9);
                                       });
                                   }

                                   Check("Warranty Card Given", invoice.WarrantyCardGiven);
                                   Check("Voucher Given to Customer", invoice.VoucherGivenToCustomer);
                                   Check("Voucher Issued by Company", invoice.VoucherIssuedByCompany);
                               });
                        });

                        col.Item().PaddingTop(14);

                        // Signatures
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().AlignCenter().Column(c =>
                            {
                                c.Item().AlignCenter().Text("________________________________");
                                c.Item().AlignCenter().Text("Customer Signature")
                                 .FontSize(9).FontColor(Color.FromHex("555555"));
                            });
                            row.RelativeItem().AlignCenter().Column(c =>
                            {
                                c.Item().AlignCenter().Text("________________________________");
                                c.Item().AlignCenter().Text("Authorized Signature")
                                 .FontSize(9).FontColor(Color.FromHex("555555"));
                            });
                        });
                    });

                    // ═══════════════ FOOTER ═══════════════
                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("Thank you for your purchase!  —  ")
                         .FontSize(8).FontColor(Color.FromHex("666666"));
                        t.Span(ShopName)
                         .FontSize(8).Bold().FontColor(Color.FromHex("1B4079"));
                        t.Span($"  |  {ShopPhone}")
                         .FontSize(8).FontColor(Color.FromHex("666666"));
                    });
                });
            }).GeneratePdf(filePath);

            // ✅ Only open if requested
            if (openAfterGenerate)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = filePath,
                    UseShellExecute = true
                });
            }
        }
    }
}