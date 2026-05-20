// Services/Pdf/TrackingPdfService.cs
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using Ramza_EBike_Swabi.Views.Pages;
using System;
using System.Diagnostics;
using System.IO;

namespace Ramza_EBike_Swabi.Services.Pdf
{
    public static class TrackingPdfService
    {
        private const string ShopName = "Swabi Enterprises";
        private const string ShopNTN = "NTN: I039290";
        private const string ShopAddress = "Main Jahangira Road Near Sadaat CNG, Swabi";
        private const string ShopPhone = "+92 345 9996397";

        public static void Generate(TrackingCardViewModel vm)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            string logoPath = Path.Combine(
                AppDomain.CurrentDomain.BaseDirectory,
                "Resources", "logo.png");

            string filePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
                $"Tracking_INV{vm.InvoiceId:D4}_{vm.CustomerName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(18);
                    page.DefaultTextStyle(x => x.FontSize(9).FontFamily("Arial"));

                    // ══════════════════════════════════════════════
                    // HEADER
                    // ══════════════════════════════════════════════
                    page.Header().Column(headerCol =>
                    {
                        headerCol.Item().Row(row =>
                        {
                            // Logo
                            row.ConstantItem(70).AlignMiddle().Element(e =>
                            {
                                if (File.Exists(logoPath))
                                    e.Image(logoPath, ImageScaling.FitArea);
                                else
                                    e.Text(string.Empty);
                            });

                            // Shop info — centre
                            row.RelativeItem().AlignCenter().AlignMiddle().Column(col =>
                            {
                                col.Item().AlignCenter()
                                   .Text(ShopName)
                                   .FontSize(18).Bold()
                                   .FontColor(Color.FromHex("1B4079"));

                                col.Item().AlignCenter()
                                   .Text(ShopNTN)
                                   .FontSize(9).FontColor(Color.FromHex("444444"));

                                col.Item().AlignCenter()
                                   .Text(ShopAddress)
                                   .FontSize(9).FontColor(Color.FromHex("444444"));

                                col.Item().AlignCenter()
                                   .Text($"Phone: {ShopPhone}")
                                   .FontSize(9).FontColor(Color.FromHex("444444"));
                            });

                            // Doc meta — right
                            row.ConstantItem(185).AlignMiddle().Column(col =>
                            {
                                col.Item().AlignRight()
                                   .Text("DOCUMENT TRACKING REPORT")
                                   .FontSize(11).Bold()
                                   .FontColor(Color.FromHex("1B4079"));

                                col.Item().PaddingTop(3).AlignRight()
                                   .Text($"Invoice #: INV-{vm.InvoiceId:D4}")
                                   .FontSize(9);

                                col.Item().AlignRight()
                                   .Text($"Invoice Date: {vm.InvoiceDate:dd-MMM-yyyy}")
                                   .FontSize(9);

                                col.Item().AlignRight()
                                   .Text($"Payment Status: {vm.Status}")
                                   .FontSize(9).Bold();

                                // All-Delivered badge
                                if (vm.AllDelivered)
                                {
                                    col.Item().PaddingTop(3).AlignRight()
                                       .Text("ALL DOCUMENTS DELIVERED")
                                       .FontSize(9).Bold()
                                       .FontColor(Color.FromHex("1B8A3D"));
                                }
                                else
                                {
                                    col.Item().PaddingTop(3).AlignRight()
                                       .Text("DOCUMENTS PENDING")
                                       .FontSize(9).Bold()
                                       .FontColor(Color.FromHex("C0392B"));
                                }
                            });
                        });

                        headerCol.Item().PaddingTop(4)
                                 .LineHorizontal(1.5f)
                                 .LineColor(Color.FromHex("1B4079"));
                    });

                    // ══════════════════════════════════════════════
                    // CONTENT
                    // ══════════════════════════════════════════════
                    page.Content().PaddingTop(8).Column(col =>
                    {
                        // ── Row 1: Customer Info + Document Status ──
                        col.Item().Row(row =>
                        {
                            // Customer details
                            row.RelativeItem()
                               .Border(0.5f).BorderColor(Color.FromHex("CCCCCC"))
                               .Background(Color.FromHex("F0F4FA"))
                               .Padding(8).Column(c =>
                               {
                                   c.Item().Text("CUSTOMER DETAILS")
                                    .FontSize(8).Bold()
                                    .FontColor(Color.FromHex("1B4079"));

                                   c.Item().PaddingTop(4).Table(t =>
                                   {
                                       t.ColumnsDefinition(cd =>
                                       {
                                           cd.ConstantColumn(85);
                                           cd.RelativeColumn();
                                       });

                                       void R(string lbl, string val)
                                       {
                                           t.Cell().PaddingBottom(2).Text(lbl).Bold().FontSize(9);
                                           t.Cell().PaddingBottom(2).Text(val ?? "-").FontSize(9);
                                       }

                                       R("Name:", vm.CustomerName);
                                       R("Father Name:", vm.FatherName);
                                       R("Contact:", vm.Contact);
                                       R("CNIC:", vm.CNIC);
                                       R("Address:", vm.Address);

                                       if (!string.IsNullOrWhiteSpace(vm.DueDateText))
                                           R("Due Date:", vm.DueDateText);

                                       if (!string.IsNullOrWhiteSpace(vm.Remarks))
                                           R("Remarks:", vm.Remarks);
                                   });
                               });

                            row.ConstantItem(8);

                            // Document status panel
                            row.ConstantItem(260)
                               .Border(0.5f).BorderColor(Color.FromHex("CCCCCC"))
                               .Background(Color.FromHex("F0F4FA"))
                               .Padding(8).Column(c =>
                               {
                                   c.Item().Text("DOCUMENT STATUS")
                                    .FontSize(8).Bold()
                                    .FontColor(Color.FromHex("1B4079"));

                                   c.Item().PaddingTop(6).Table(t =>
                                   {
                                       t.ColumnsDefinition(cd =>
                                       {
                                           cd.RelativeColumn(2f);   // Document name
                                           cd.RelativeColumn(1.2f); // Status
                                           cd.RelativeColumn(2f);   // Detail
                                       });

                                       // Header
                                       void TH(string txt) =>
                                           t.Cell()
                                            .Background(Color.FromHex("1B4079"))
                                            .Padding(4)
                                            .Text(txt).FontSize(8).Bold()
                                            .FontColor(Colors.White);

                                       TH("Document"); TH("Status"); TH("Delivered");

                                       // Row helper
                                       void DocRow(string name, bool delivered, string detail)
                                       {
                                           string statusText = delivered ? "Delivered" : "Pending";
                                           string statusHex = delivered ? "1B8A3D" : "C0392B";
                                           string bgHex = delivered ? "EAF7EE" : "FDF0EF";

                                           t.Cell()
                                            .Background(Color.FromHex(bgHex))
                                            .BorderBottom(0.3f).BorderColor(Color.FromHex("DDDDDD"))
                                            .Padding(4)
                                            .Text(name).FontSize(9).Bold();

                                           t.Cell()
                                            .Background(Color.FromHex(bgHex))
                                            .BorderBottom(0.3f).BorderColor(Color.FromHex("DDDDDD"))
                                            .Padding(4)
                                            .Text(statusText).FontSize(9).Bold()
                                            .FontColor(Color.FromHex(statusHex));

                                           t.Cell()
                                            .Background(Color.FromHex(bgHex))
                                            .BorderBottom(0.3f).BorderColor(Color.FromHex("DDDDDD"))
                                            .Padding(4)
                                            .Text(detail).FontSize(8)
                                            .FontColor(Color.FromHex("555555"));
                                       }

                                       DocRow("Warranty Card", vm.WarrantyDelivered, vm.WarrantyStatus);
                                       DocRow("Voucher (Customer)", vm.VoucherCustDelivered, vm.VoucherCustStatus);
                                       DocRow("Voucher (Company)", vm.VoucherCompDelivered, vm.VoucherCompStatus);
                                   });

                                   // All-delivered badge inside panel
                                   if (vm.AllDelivered)
                                   {
                                       c.Item().PaddingTop(6)
                                        .Background(Color.FromHex("D1FAE5"))
                                        .Padding(5)
                                        .Text("All documents have been delivered to the customer.")
                                        .FontSize(8).Bold()
                                        .FontColor(Color.FromHex("065F46"));
                                   }
                               });
                        });

                        col.Item().PaddingTop(8);

                        // ── Bike Details ──────────────────────────
                        if (vm.BikesSummary.Count > 0)
                        {
                            col.Item().Text("BIKE DETAILS")
                               .FontSize(8).Bold()
                               .FontColor(Color.FromHex("1B4079"));

                            col.Item().PaddingTop(3)
                               .Border(0.5f).BorderColor(Color.FromHex("CCCCCC"))
                               .Table(t =>
                               {
                                   t.ColumnsDefinition(c =>
                                   {
                                       c.ConstantColumn(22);   // #
                                       c.RelativeColumn(2f);   // Model • Brand
                                       c.RelativeColumn(2f);   // Motor No.
                                       c.RelativeColumn(2f);   // Chassis No.
                                       c.RelativeColumn(1.5f); // Color | Power
                                   });

                                   t.Header(h =>
                                   {
                                       void H(string txt) =>
                                           h.Cell()
                                            .Background(Color.FromHex("1B4079"))
                                            .Padding(5)
                                            .Text(txt).FontSize(8).Bold()
                                            .FontColor(Colors.White);

                                       H("#"); H("Model  •  Brand"); H("Motor Number");
                                       H("Chassis Number"); H("Color  |  Power");
                                   });

                                   int idx = 1;
                                   bool alt = false;
                                   foreach (var b in vm.BikesSummary)
                                   {
                                       var bg = alt ? Color.FromHex("F5F8FF") : Colors.White;
                                       alt = !alt;

                                       void Cell(string val) =>
                                           t.Cell().Background(bg)
                                            .BorderBottom(0.3f)
                                            .BorderColor(Color.FromHex("DDDDDD"))
                                            .Padding(4)
                                            .Text(val ?? "-").FontSize(9);

                                       Cell(idx++.ToString());
                                       Cell(b.ModelBrand);
                                       Cell(b.MotorNumber);
                                       Cell(b.ChassisNumber);
                                       Cell(b.ColorPower);
                                   }
                               });

                            col.Item().PaddingTop(8);
                        }

                        // ── Issuance History ──────────────────────
                        if (vm.HistoryRows.Count > 0)
                        {
                            col.Item().Text("ISSUANCE HISTORY")
                               .FontSize(8).Bold()
                               .FontColor(Color.FromHex("1B4079"));

                            col.Item().PaddingTop(3)
                               .Border(0.5f).BorderColor(Color.FromHex("CCCCCC"))
                               .Table(t =>
                               {
                                   t.ColumnsDefinition(c =>
                                   {
                                       c.RelativeColumn(1.6f); // Date
                                       c.RelativeColumn(0.8f); // Type
                                       c.RelativeColumn(2f);   // Documents
                                       c.RelativeColumn(1.3f); // Issued By
                                       c.RelativeColumn(1.3f); // Received By
                                       c.RelativeColumn(2f);   // Notes
                                   });

                                   t.Header(h =>
                                   {
                                       void H(string txt) =>
                                           h.Cell()
                                            .Background(Color.FromHex("1B4079"))
                                            .Padding(5)
                                            .Text(txt).FontSize(8).Bold()
                                            .FontColor(Colors.White);

                                       H("Date & Time"); H("Type"); H("Documents Issued");
                                       H("Issued By"); H("Received By"); H("Notes");
                                   });

                                   foreach (var row in vm.HistoryRows)
                                   {
                                       // Billing rows get a blue tint; post-billing rows alternate white/light
                                       var bg = row.IsBillingEntry
                                           ? Color.FromHex("DBEAFE")   // light blue for billing
                                           : Colors.White;

                                       void Cell(string val) =>
                                           t.Cell().Background(bg)
                                            .BorderBottom(0.3f)
                                            .BorderColor(Color.FromHex("DDDDDD"))
                                            .Padding(4)
                                            .Text(val ?? "-").FontSize(9);

                                       Cell(row.DateDisplay);
                                       Cell(row.RowLabel);
                                       Cell(row.DocumentsIssued);
                                       Cell(row.IssuedBy);
                                       Cell(row.ReceivedBy);
                                       Cell(row.Notes);
                                   }
                               });
                        }
                    });

                    // ══════════════════════════════════════════════
                    // FOOTER
                    // ══════════════════════════════════════════════
                    page.Footer().AlignCenter().Text(t =>
                    {
                        t.Span("Document Tracking Report  —  ")
                         .FontSize(8).FontColor(Color.FromHex("666666"));
                        t.Span(ShopName)
                         .FontSize(8).Bold().FontColor(Color.FromHex("1B4079"));
                        t.Span($"  |  {ShopPhone}  |  Generated: {DateTime.Now:dd MMM yyyy  hh:mm tt}")
                         .FontSize(8).FontColor(Color.FromHex("666666"));
                    });
                });
            }).GeneratePdf(filePath);

            Process.Start(new ProcessStartInfo
            {
                FileName = filePath,
                UseShellExecute = true
            });
        }
    }
}