using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using Ramza_EBike_Swabi.Models;
using Ramza_EBike_Swabi.Services;
using ClosedXML.Excel;
using Microsoft.Win32;

namespace Ramza_EBike_Swabi.Views.Pages
{
    public partial class BikePage : Page
    {
        private readonly MainLayout? _mainLayout;
        private readonly BikeService _bikeService = new();
        private readonly DispatcherTimer _searchDebounceTimer;

        public BikePage()
        {
            InitializeComponent();
            _searchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(350)
            };
            _searchDebounceTimer.Tick += SearchDebounceTimer_Tick;
            _ = LoadBikesAsync();
        }

        public BikePage(MainLayout mainLayout) : this()
            => _mainLayout = mainLayout;

        // ── Search ──────────────────────────────────────────────
        private async void SearchDebounceTimer_Tick(object? sender, EventArgs e)
        {
            _searchDebounceTimer.Stop();
            await LoadBikesAsync(SearchBox.Text);
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchDebounceTimer.Stop();
            if (string.IsNullOrWhiteSpace(SearchBox.Text))
            {
                _ = LoadBikesAsync();
                return;
            }
            _searchDebounceTimer.Start();
        }

        private async void Search_Click(object sender, RoutedEventArgs e)
        {
            _searchDebounceTimer.Stop();
            await LoadBikesAsync(SearchBox.Text);
        }

        // ── Load ─────────────────────────────────────────────────
        private async Task LoadBikesAsync(string? search = null)
        {
            try
            {
                BikesGrid.IsEnabled = false;
                var list = await _bikeService.GetAllBikesAsync(search);
                if (string.IsNullOrWhiteSpace(search))
                    list = list.OrderBy(b => b.Model).ToList();
                BikesGrid.ItemsSource = list;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to load bikes: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                BikesGrid.IsEnabled = true;
            }
        }

        // ── Navigation ───────────────────────────────────────────
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (Window.GetWindow(this) is MainLayout layout)
                layout.Navigate(new DashboardPage(layout));
        }

        // ── QR ───────────────────────────────────────────────────
        private void QR_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is not Bike bike) return;
            var win = new QRCodeWindow(bike) { Owner = Window.GetWindow(this) };
            win.ShowDialog();
        }

        // ── Delete Bike ──────────────────────────────────────────────
        private async void Delete_Click(object sender, RoutedEventArgs e)
        {
            if (((FrameworkElement)sender).DataContext is not Bike bike)
                return;

            // Confirmation dialog
            var result = MessageBox.Show(
                $"Are you sure you want to delete this bike?\n\n" +
                $"Model: {bike.Model}\n" +
                $"ID: {bike.BikeId}\n\n" +
                "This action cannot be undone.",
                "Confirm Delete",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                bool success = await _bikeService.DeleteBikeAsync(bike.BikeId);

                if (success)
                {
                    MessageBox.Show("Bike deleted successfully.", "Success",
                        MessageBoxButton.OK, MessageBoxImage.Information);

                    // Refresh the grid
                    await LoadBikesAsync(SearchBox.Text);
                }
                else
                {
                    MessageBox.Show("Failed to delete bike. It may not exist or is in use.",
                        "Delete Failed", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting bike: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ── Print / Export ───────────────────────────────────────
        private async void PrintList_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var bikes = await _bikeService.GetAllBikesAsync();

                if (bikes == null || bikes.Count == 0)
                {
                    MessageBox.Show("No bikes available to export.");
                    return;
                }

                var saveDialog = new SaveFileDialog
                {
                    Filter = "Excel Files (*.xlsx)|*.xlsx",
                    FileName = $"Available_Bike_List_{DateTime.Now:yyyyMMdd}.xlsx"
                };

                if (saveDialog.ShowDialog() != true) return;

                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Bikes");

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

                int totalCols = 12;
                int row = 1;

                // ── Row 1: Company Title ─────────────────────────────────
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

                // ── Row 2: Report Subtitle ───────────────────────────────
                var subRange = ws.Range(row, 1, row, totalCols);
                subRange.Merge();
                subRange.Value = "Available Bike Stock Report";
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

                // ── Row 4: Summary Block ─────────────────────────────────
                int totalQty = bikes.Sum(b => b.Quantity);
                decimal totalWorth = bikes.Sum(b => b.Quantity * b.Price);
                decimal zakat = totalWorth * 0.025m;

                // Summary label + value pairs side by side
                string[] summaryLabels = { "Total Bikes in Stock", "Total Worth (₨)", "Zakat 2.5% (₨)" };
                string[] summaryValues =
                {
            totalQty.ToString("N0"),
            totalWorth.ToString("N0"),
            zakat.ToString("N0")
        };

                row++; // spacer
                for (int s = 0; s < summaryLabels.Length; s++)
                {
                    // Label cell (cols 1–3)
                    var labelRange = ws.Range(row, 1, row, 3);
                    labelRange.Merge();
                    labelRange.Value = summaryLabels[s];
                    labelRange.Style
                        .Font.SetBold(true)
                        .Font.SetFontSize(10)
                        .Font.SetFontColor(totalFg)
                        .Fill.SetBackgroundColor(totalBg)
                        .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left)
                        .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                        .Border.SetOutsideBorderColor(borderColor);

                    // Value cell (cols 4–6)
                    var valueRange = ws.Range(row, 4, row, 6);
                    valueRange.Merge();
                    valueRange.Value = summaryValues[s];
                    valueRange.Style
                        .Font.SetBold(true)
                        .Font.SetFontSize(10)
                        .Font.SetFontColor(totalFg)
                        .Fill.SetBackgroundColor(totalBg)
                        .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right)
                        .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                        .Border.SetOutsideBorderColor(borderColor);

                    ws.Row(row).Height = 18;
                    row++;
                }

                row++; // spacer before table

                // ── Column Headers ───────────────────────────────────────
                int headerRow = row;
                string[] headers =
                {
            "ID", "Model", "Brand", "Motor Power", "Motor No",
            "Chassis No", "Battery", "Color", "Warranty",
            "Qty", "Price (₨)", "Vendor"
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

                foreach (var b in bikes)
                {
                    var rowBg = alternate ? altRowBg : XLColor.White;

                    object[] values =
                    {
                b.BikeId,
                b.Model         ?? "-",
                b.Brand         ?? "-",
                b.MotorPower    ?? "-",
                b.MotorNumber   ?? "-",
                b.ChassisNumber ?? "-",
                b.BatteryCapacity ?? "-",
                b.Color         ?? "-",
                b.Warranty      ?? "-",
                b.Quantity,
                b.Price,
                b.VendorName    ?? "-"
            };

                    for (int col = 1; col <= values.Length; col++)
                    {
                        var cell = ws.Cell(row, col);
                        cell.Value = XLCellValue.FromObject(values[col - 1]);
                        cell.Style
                            .Fill.SetBackgroundColor(rowBg)
                            .Font.SetFontSize(10)
                            .Border.SetOutsideBorder(XLBorderStyleValues.Thin)
                            .Border.SetOutsideBorderColor(borderColor);

                        // Right-align numeric columns: Qty (10), Price (11)
                        if (col == 10 || col == 11)
                        {
                            cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
                            if (col == 11)
                                cell.Style.NumberFormat.Format = "#,##0";
                        }
                        else
                        {
                            cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);
                        }
                    }

                    ws.Row(row).Height = 18;
                    alternate = !alternate;
                    row++;
                }

                int dataEndRow = row - 1;

                // ── Grand Total Row ──────────────────────────────────────
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

                // Qty sum (col 10)
                var qtyCell = ws.Cell(row, 10);
                qtyCell.FormulaA1 = $"=SUM(J{dataStartRow}:J{dataEndRow})";
                qtyCell.Style
                    .Font.SetBold(true).Font.SetFontSize(11).Font.SetFontColor(totalFg)
                    .Fill.SetBackgroundColor(totalBg)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right)
                    .Border.SetOutsideBorder(XLBorderStyleValues.Medium)
                    .Border.SetOutsideBorderColor(XLColor.FromHtml("#2B579A"));

                // Price total (col 11) — sum of all prices
                var priceCell = ws.Cell(row, 11);
                priceCell.FormulaA1 = $"=SUMPRODUCT(J{dataStartRow}:J{dataEndRow},K{dataStartRow}:K{dataEndRow})";
                priceCell.Style
                    .Font.SetBold(true).Font.SetFontSize(11).Font.SetFontColor(totalFg)
                    .Fill.SetBackgroundColor(totalBg)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right)
                    .Border.SetOutsideBorder(XLBorderStyleValues.Medium)
                    .Border.SetOutsideBorderColor(XLColor.FromHtml("#2B579A"));
                priceCell.Style.NumberFormat.Format = "#,##0";

                // Empty vendor cell in total row
                var vendorTotalCell = ws.Cell(row, 12);
                vendorTotalCell.Style
                    .Fill.SetBackgroundColor(totalBg)
                    .Border.SetOutsideBorder(XLBorderStyleValues.Medium)
                    .Border.SetOutsideBorderColor(XLColor.FromHtml("#2B579A"));

                ws.Row(row).Height = 22;

                // ── Column Widths ────────────────────────────────────────
                ws.Column(1).Width = 6;    // ID
                ws.Column(2).Width = 18;   // Model
                ws.Column(3).Width = 12;   // Brand
                ws.Column(4).Width = 14;   // Motor Power
                ws.Column(5).Width = 16;   // Motor No
                ws.Column(6).Width = 16;   // Chassis No
                ws.Column(7).Width = 12;   // Battery
                ws.Column(8).Width = 10;   // Color
                ws.Column(9).Width = 12;   // Warranty
                ws.Column(10).Width = 6;    // Qty
                ws.Column(11).Width = 14;   // Price
                ws.Column(12).Width = 18;   // Vendor

                // ── Freeze header row ────────────────────────────────────
                ws.SheetView.FreezeRows(headerRow);

                wb.SaveAs(saveDialog.FileName);
                MessageBox.Show("Bike list exported successfully!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}