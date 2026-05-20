using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using ClosedXML.Excel;
using Ramza_EBike_Swabi.Services;
using Ramza_EBike_Swabi.Services.Pdf;
using Ramza_EBike_Swabi.Models;

namespace Ramza_EBike_Swabi.Views.Pages
{
    public partial class SearchInvoicePage : Page
    {
        private readonly InvoiceService _service = new();

        // Keep reference so we can navigate via MainLayout (gives Back button)
        private MainLayout? _layout;

        public SearchInvoicePage()
        {
            InitializeComponent();
        }

        public SearchInvoicePage(MainLayout layout) : this()
        {
            _layout = layout;
        }

        // ===========================
        // SEARCH
        // ===========================
        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var keyword = txtSearch.Text.Trim();
                var invoices = await _service.GetAllInvoicesAsync();

                DateTime from = dpFrom.SelectedDate ?? DateTime.MinValue;
                DateTime to = dpTo.SelectedDate ?? DateTime.MaxValue;

                dgInvoices.ItemsSource = invoices
                    .Where(i => i.Status == "Clear")
                    .Where(i => i.InvoiceDate.Date >= from.Date &&
                                i.InvoiceDate.Date <= to.Date)
                    .Where(i => string.IsNullOrEmpty(keyword) ||
                                (i.Customer?.Name?.Contains(
                                    keyword, StringComparison.OrdinalIgnoreCase) == true) ||
                                (i.Customer?.CNIC?.Contains(keyword) == true) ||
                                i.CustomerInvoiceId.ToString().Contains(keyword))
                    .ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Search error: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            txtSearch.Clear();
            dpFrom.SelectedDate = null;
            dpTo.SelectedDate = null;
            dgInvoices.ItemsSource = null;
        }

        // ===========================
        // BACK
        // ===========================
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            var layout = _layout ?? Window.GetWindow(this) as MainLayout;
            layout?.Navigate(new DashboardPage(layout));
        }

        // ===========================
        // PDF — fixed: null-check invoice and items before generating
        // ===========================
        private void Pdf_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not CustomerInvoice invoice)
                return;

            // Guard: ensure Customer is not null (avoid crash)
            if (invoice.Customer == null)
            {
                MessageBox.Show("Invoice has no customer data. Cannot generate PDF.",
                    "Warning", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                InvoicePdfService.Generate(invoice);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"PDF generation failed:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===========================
        // EDIT — navigates via MainLayout so Back button works
        // ===========================
        private void Edit_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not CustomerInvoice invoice)
                return;

            var layout = _layout ?? Window.GetWindow(this) as MainLayout;

            if (layout != null)
            {
                // Use InvoiceTabManager so edit opens in a tab with Back button
                if (layout.InvoiceTabManager == null)
                    layout.InvoiceTabManager = new InvoiceTabManagerPage(layout);

                // Navigate to tab manager first
                layout.Navigate(layout.InvoiceTabManager);

                // Then open invoice in a new tab
                _ = layout.InvoiceTabManager.OpenInvoiceForEditAsync(invoice);
            }
            else
            {
                // Fallback — direct navigation
                NavigationService?.Navigate(new GenerateInvoicePage(invoice));
            }
        }
        // ===========================
        // HISTORY
        // ===========================
        private void History_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not CustomerInvoice invoice) return;

            var win = new PaymentHistoryWindow(invoice)
            {
                Owner = Window.GetWindow(this)
            };
            win.ShowDialog();
        }

        // ===========================
        // DOWNLOAD EXCEL
        // ===========================
        private async void Download_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var invoices = await _service.GetAllInvoicesAsync();
                DateTime from = dpFrom.SelectedDate ?? DateTime.MinValue;
                DateTime to = dpTo.SelectedDate ?? DateTime.MaxValue;

                var filtered = invoices
                    .Where(i => i.Status == "Clear")
                    .Where(i => i.InvoiceDate.Date >= from.Date &&
                                i.InvoiceDate.Date <= to.Date)
                    .ToList();

                if (!filtered.Any())
                {
                    MessageBox.Show("No records found in selected date range.");
                    return;
                }

                var dialog = new SaveFileDialog
                {
                    Filter = "Excel File (*.xlsx)|*.xlsx",
                    FileName = $"BikeSales_{DateTime.Now:yyyyMMdd}.xlsx"
                };

                if (dialog.ShowDialog() != true) return;

                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Bike Sales");

                // ── Palette ──────────────────────────────────────────────
                var headerBg = XLColor.FromHtml("#2B579A");
                var headerFg = XLColor.White;
                var titleBg = XLColor.FromHtml("#1E3A6E");
                var titleFg = XLColor.White;
                var subtitleBg = XLColor.FromHtml("#D6E4F0");
                var altRowBg = XLColor.FromHtml("#F2F7FB");
                var totalBg = XLColor.FromHtml("#E8F0FE");
                var totalFg = XLColor.FromHtml("#1A237E");
                var borderColor = XLColor.FromHtml("#B0BEC5");

                int totalCols = 12; // A–L

                // ── Row 1: Company Title ─────────────────────────────────
                int row = 1;
                var titleRange = ws.Range(row, 1, row, totalCols);
                titleRange.Merge();
                titleRange.Value = "Swabi Enterprises";
                titleRange.Style
                    .Font.SetBold(true)
                    .Font.SetFontSize(16)
                    .Font.SetFontColor(titleFg)
                    .Fill.SetBackgroundColor(titleBg)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                    .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
                ws.Row(row).Height = 28;
                row++;

                // ── Row 2: Date Range Subtitle ───────────────────────────
                string fromText = dpFrom.SelectedDate.HasValue
                    ? dpFrom.SelectedDate.Value.ToString("dd-MMM-yyyy") : "Start";
                string toText = dpTo.SelectedDate.HasValue
                    ? dpTo.SelectedDate.Value.ToString("dd-MMM-yyyy") : "End";

                var subRange = ws.Range(row, 1, row, totalCols);
                subRange.Merge();
                subRange.Value = $"Sales Report  |  {fromText}  →  {toText}";
                subRange.Style
                    .Font.SetBold(false)
                    .Font.SetFontSize(11)
                    .Font.SetFontColor(XLColor.FromHtml("#1E3A6E"))
                    .Fill.SetBackgroundColor(subtitleBg)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                    .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
                ws.Row(row).Height = 22;
                row++;

                // ── Row 3: Generated On ──────────────────────────────────
                var genRange = ws.Range(row, 1, row, totalCols);
                genRange.Merge();
                genRange.Value = $"Generated on: {DateTime.Now:dd-MMM-yyyy  HH:mm}";
                genRange.Style
                    .Font.SetFontSize(9)
                    .Font.SetItalic(true)
                    .Font.SetFontColor(XLColor.Gray)
                    .Fill.SetBackgroundColor(XLColor.FromHtml("#FAFAFA"))
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
                ws.Row(row).Height = 16;
                row++;

                // ── Blank spacer ─────────────────────────────────────────
                row++;

                // ── Row 5: Column Headers ────────────────────────────────
                int headerRow = row;
                string[] headers =
                {
            "Invoice #", "Invoice Date", "Customer Name", "CNIC",
            "Contact", "Bike Model", "Brand", "Motor No",
            "Chassis No", "Price (₨)", "Qty", "Total (₨)"
        };

                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = ws.Cell(row, i + 1);
                    cell.Value = headers[i];
                    cell.Style
                        .Font.SetBold(true)
                        .Font.SetFontSize(10)
                        .Font.SetFontColor(headerFg)
                        .Fill.SetBackgroundColor(headerBg)
                        .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                        .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                        .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                        .Border.SetOutsideBorderColor(borderColor);
                }
                ws.Row(row).Height = 20;
                row++;

                // ── Data Rows ────────────────────────────────────────────
                int dataStartRow = row;
                bool alternate = false;

                foreach (var invoice in filtered)
                {
                    foreach (var item in invoice.Items
                        .Where(i => !string.IsNullOrWhiteSpace(i.MotorNumber)))
                    {
                        var rowBg = alternate ? altRowBg : XLColor.White;

                        object[] values =
                        {
                    invoice.CustomerInvoiceId,
                    invoice.InvoiceDate.ToString("dd-MMM-yyyy"),
                    invoice.Customer?.Name  ?? "-",
                    invoice.Customer?.CNIC  ?? "-",
                    invoice.Customer?.Contact ?? "-",
                    item.Model,
                    item.Brand,
                    item.MotorNumber,
                    item.ChassisNumber,
                    item.Price,
                    item.Quantity,
                    item.TotalPrice
                };

                        for (int col = 1; col <= values.Length; col++)
                        {
                            var cell = ws.Cell(row, col);
                            cell.Value = values[col - 1] is decimal d
                                ? XLCellValue.FromObject(d)
                                : XLCellValue.FromObject(values[col - 1]);

                            cell.Style
                                .Fill.SetBackgroundColor(rowBg)
                                .Font.SetFontSize(10)
                                .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                                .Border.SetOutsideBorderColor(borderColor);

                            // Right-align numeric columns
                            if (col == 10 || col == 11 || col == 12)
                                cell.Style.Alignment.SetHorizontal(
                                    XLAlignmentHorizontalValues.Right);
                            else
                                cell.Style.Alignment.SetHorizontal(
                                    XLAlignmentHorizontalValues.Left);

                            // Currency format for Price & Total
                            if (col == 10 || col == 12)
                                cell.Style.NumberFormat.Format = "#,##0";
                        }

                        ws.Row(row).Height = 18;
                        alternate = !alternate;
                        row++;
                    }
                }

                int dataEndRow = row - 1;

                // ── Totals Row ───────────────────────────────────────────
                var totalLabelRange = ws.Range(row, 1, row, 9);
                totalLabelRange.Merge();
                totalLabelRange.Value = "GRAND TOTAL";
                totalLabelRange.Style
                    .Font.SetBold(true)
                    .Font.SetFontSize(11)
                    .Font.SetFontColor(totalFg)
                    .Fill.SetBackgroundColor(totalBg)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right)
                    .Border.SetOutsideBorder(XLBorderStyleValues.Medium)
                    .Border.SetOutsideBorderColor(XLColor.FromHtml("#2B579A"));

                // Qty sum (col 11)
                var qtyCell = ws.Cell(row, 11);
                qtyCell.FormulaA1 = $"=SUM(K{dataStartRow}:K{dataEndRow})";
                qtyCell.Style
                    .Font.SetBold(true).Font.SetFontSize(11).Font.SetFontColor(totalFg)
                    .Fill.SetBackgroundColor(totalBg)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right)
                    .Border.SetOutsideBorder(XLBorderStyleValues.Medium)
                    .Border.SetOutsideBorderColor(XLColor.FromHtml("#2B579A"));

                // Total sum (col 12)
                var totalCell = ws.Cell(row, 12);
                totalCell.FormulaA1 = $"=SUM(L{dataStartRow}:L{dataEndRow})";
                totalCell.Style
                    .Font.SetBold(true).Font.SetFontSize(11).Font.SetFontColor(totalFg)
                    .Fill.SetBackgroundColor(totalBg)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right)
                    .NumberFormat.Format = "#,##0";
                totalCell.Style
                    .Border.SetOutsideBorder(XLBorderStyleValues.Medium)
                    .Border.SetOutsideBorderColor(XLColor.FromHtml("#2B579A"));

                ws.Row(row).Height = 22;

                // ── Column Widths ────────────────────────────────────────
                ws.Column(1).Width = 10;   // Invoice #
                ws.Column(2).Width = 15;   // Date
                ws.Column(3).Width = 22;   // Customer Name
                ws.Column(4).Width = 18;   // CNIC
                ws.Column(5).Width = 14;   // Contact
                ws.Column(6).Width = 16;   // Bike Model
                ws.Column(7).Width = 12;   // Brand
                ws.Column(8).Width = 16;   // Motor No
                ws.Column(9).Width = 16;   // Chassis No
                ws.Column(10).Width = 13;   // Price
                ws.Column(11).Width = 6;    // Qty
                ws.Column(12).Width = 14;   // Total

                // ── Freeze header row ────────────────────────────────────
                ws.SheetView.FreezeRows(headerRow);

                wb.SaveAs(dialog.FileName);
                MessageBox.Show("Excel file downloaded successfully.", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Download failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}