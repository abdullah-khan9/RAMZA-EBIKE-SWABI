using Ramza_EBike_Swabi.Services;
using Ramza_EBike_Swabi.Models;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Collections.Generic;
using System.Threading.Tasks;
using ClosedXML.Excel;
using Microsoft.Win32;
using Ramza_EBike_Swabi.Views.Windows;

namespace Ramza_EBike_Swabi.Views.Pages
{
    public partial class VendorPaymentHistoryPage : Page
    {
        private readonly MainLayout _layout;
        private readonly int _vendorId;
        private readonly string _vendorName;
        private readonly VendorService _vendorService = new();
        private readonly VendorService _service = new();
        private List<VendorPaymentRecord> _allPayments = new();

        public VendorPaymentHistoryPage(MainLayout layout, int vendorId, string vendorName)
        {
            InitializeComponent();
            _layout = layout;
            _vendorId = vendorId;
            _vendorName = vendorName;
            PageTitle.Text = $"Payment History — {vendorName}";

            ToDatePicker.SelectedDate = DateTime.Now;
            FromDatePicker.SelectedDate = DateTime.Now.AddDays(-30);

            LoadAsync();
        }

        private async void LoadAsync()
        {
            try
            {
                var payments = await _vendorService.GetVendorPaymentHistoryAsync(_vendorId);
                _allPayments = payments;
                await ApplyDateFilterAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading payment history: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async Task ApplyDateFilterAsync()
        {
            try
            {
                var filteredPayments = _allPayments.AsEnumerable();

                if (FromDatePicker.SelectedDate.HasValue)
                {
                    var fromDate = FromDatePicker.SelectedDate.Value.Date;
                    filteredPayments = filteredPayments.Where(p => p.PaymentDate >= fromDate);
                }

                if (ToDatePicker.SelectedDate.HasValue)
                {
                    var toDate = ToDatePicker.SelectedDate.Value.Date.AddDays(1).AddSeconds(-1);
                    filteredPayments = filteredPayments.Where(p => p.PaymentDate <= toDate);
                }

                var paymentsList = filteredPayments.ToList();
                PaymentsGrid.ItemsSource = paymentsList;

                TotalPaidText.Text = $"PKR {paymentsList.Sum(p => p.AmountPaid):N2}";
                PaymentCountText.Text = paymentsList.Count.ToString();

                var latest = paymentsList.OrderByDescending(p => p.PaymentDate).FirstOrDefault();
                LastPaymentText.Text = latest != null
                    ? $"PKR {latest.AmountPaid:N2} on {latest.PaymentDate:dd-MMM-yyyy}"
                    : "No payments in selected range";

                decimal remaining = await _service.GetTrueRemainingBalanceAsync(_vendorId);
                CurrentRemainingText.Text = $"PKR {remaining:N2}";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error applying filter: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===========================
        // DELETE PAYMENT
        // Removes the VendorPaymentRecord row.
        // GetTrueRemainingBalanceAsync automatically reflects the change
        // since it sums all remaining VendorPaymentRecords.
        // ===========================
        private async void DeletePayment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not VendorPaymentRecord record) return;

            var confirm = MessageBox.Show(
                $"Are you sure you want to delete this payment?\n\n" +
                $"  Amount  : PKR {record.AmountPaid:N2}\n" +
                $"  Date    : {record.PaymentDate:dd-MMM-yyyy}\n" +
                $"  Paid To : {record.PaidTo}\n\n" +
                $"Deleting this will restore the outstanding balance by PKR {record.AmountPaid:N2}.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                await _vendorService.DeleteVendorPaymentAsync(record.Id);

                MessageBox.Show("Payment deleted. Outstanding balance updated.",
                    "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);

                // Reload full list so all summary fields refresh
                var payments = await _vendorService.GetVendorPaymentHistoryAsync(_vendorId);
                _allPayments = payments;
                await ApplyDateFilterAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting payment: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===========================
        // EDIT PAYMENT
        // Opens EditVendorPaymentWindow, saves changes, reloads.
        // ===========================
        private async void EditPayment_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not VendorPaymentRecord record) return;

            var win = new EditVendorPaymentWindow(record)
            {
                Owner = Window.GetWindow(this)
            };

            if (win.ShowDialog() == true)
            {
                try
                {
                    await _vendorService.UpdateVendorPaymentAsync(win.UpdatedRecord);

                    MessageBox.Show("Payment updated. Outstanding balance recalculated.",
                        "Updated", MessageBoxButton.OK, MessageBoxImage.Information);

                    var payments = await _vendorService.GetVendorPaymentHistoryAsync(_vendorId);
                    _allPayments = payments;
                    await ApplyDateFilterAsync();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error updating payment: {ex.Message}", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        // ===========================
        // FILTER / CLEAR
        // ===========================
        private async void Filter_Click(object sender, RoutedEventArgs e)
            => await ApplyDateFilterAsync();

        private async void ClearFilter_Click(object sender, RoutedEventArgs e)
        {
            FromDatePicker.SelectedDate = null;
            ToDatePicker.SelectedDate = null;
            PaymentsGrid.ItemsSource = _allPayments;

            TotalPaidText.Text = $"PKR {_allPayments.Sum(p => p.AmountPaid):N2}";
            PaymentCountText.Text = _allPayments.Count.ToString();

            var latest = _allPayments.OrderByDescending(p => p.PaymentDate).FirstOrDefault();
            LastPaymentText.Text = latest != null
                ? $"PKR {latest.AmountPaid:N2} on {latest.PaymentDate:dd-MMM-yyyy}"
                : "No payments yet";

            decimal remaining = await _service.GetTrueRemainingBalanceAsync(_vendorId);
            CurrentRemainingText.Text = $"PKR {remaining:N2}";
        }

        // ===========================
        // EXPORT TO EXCEL
        // ===========================
        private async void ExportToExcel_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var payments = PaymentsGrid.ItemsSource as List<VendorPaymentRecord>;

                if (payments == null || payments.Count == 0)
                {
                    MessageBox.Show("No payment records available to export.", "No Data",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                var saveDialog = new SaveFileDialog
                {
                    Filter = "Excel Files (*.xlsx)|*.xlsx",
                    FileName = $"Vendor_Payment_History_{_vendorName.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd}.xlsx"
                };

                if (saveDialog.ShowDialog() != true) return;

                using var wb = new XLWorkbook();
                var ws = wb.Worksheets.Add("Payment History");

                var headerBg = XLColor.FromHtml("#2B579A");
                var headerFg = XLColor.White;
                var titleBg = XLColor.FromHtml("#1E3A6E");
                var titleFg = XLColor.White;
                var subtitleBg = XLColor.FromHtml("#D6E4F0");
                var altRowBg = XLColor.FromHtml("#F2F7FB");
                var totalBg = XLColor.FromHtml("#E8F0FE");
                var totalFg = XLColor.FromHtml("#1A237E");
                var borderColor = XLColor.FromHtml("#B0BEC5");

                int totalCols = 8;
                int row = 1;

                var titleRange = ws.Range(row, 1, row, totalCols);
                titleRange.Merge();
                titleRange.Value = "Swabi Enterprises";
                titleRange.Style
                    .Font.SetBold(true).Font.SetFontSize(16).Font.SetFontColor(titleFg)
                    .Fill.SetBackgroundColor(titleBg)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                    .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
                ws.Row(row).Height = 28;
                row++;

                var subRange = ws.Range(row, 1, row, totalCols);
                subRange.Merge();
                subRange.Value = $"Vendor Payment History Report — {_vendorName}";
                subRange.Style
                    .Font.SetFontSize(11).Font.SetFontColor(XLColor.FromHtml("#1E3A6E"))
                    .Fill.SetBackgroundColor(subtitleBg)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                    .Alignment.SetVertical(XLAlignmentVerticalValues.Center);
                ws.Row(row).Height = 22;
                row++;

                var genRange = ws.Range(row, 1, row, totalCols);
                genRange.Merge();
                string dateRangeText = "";
                if (FromDatePicker.SelectedDate.HasValue || ToDatePicker.SelectedDate.HasValue)
                {
                    string from = FromDatePicker.SelectedDate?.ToString("dd-MMM-yyyy") ?? "Start";
                    string to = ToDatePicker.SelectedDate?.ToString("dd-MMM-yyyy") ?? "End";
                    dateRangeText = $"Period: {from} to {to} | ";
                }
                genRange.Value = $"{dateRangeText}Generated on: {DateTime.Now:dd-MMM-yyyy  HH:mm}";
                genRange.Style
                    .Font.SetFontSize(9).Font.SetItalic(true).Font.SetFontColor(XLColor.Gray)
                    .Fill.SetBackgroundColor(XLColor.FromHtml("#FAFAFA"))
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
                ws.Row(row).Height = 16;
                row++;

                decimal totalPaid = payments.Sum(p => p.AmountPaid);
                int paymentCount = payments.Count;
                decimal currentBalance = await _service.GetTrueRemainingBalanceAsync(_vendorId);

                string[] summaryLabels = { "Total Payments Made (₨)", "Number of Payments", "Current Balance Due (₨)" };
                string[] summaryValues = { totalPaid.ToString("N2"), paymentCount.ToString("N0"), currentBalance.ToString("N2") };

                row++;
                for (int s = 0; s < summaryLabels.Length; s++)
                {
                    var lbl = ws.Range(row, 1, row, 3);
                    lbl.Merge(); lbl.Value = summaryLabels[s];
                    lbl.Style.Font.SetBold(true).Font.SetFontSize(10).Font.SetFontColor(totalFg)
                        .Fill.SetBackgroundColor(totalBg)
                        .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left)
                        .Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorderColor(borderColor);

                    var val = ws.Range(row, 4, row, 6);
                    val.Merge(); val.Value = summaryValues[s];
                    val.Style.Font.SetBold(true).Font.SetFontSize(10).Font.SetFontColor(totalFg)
                        .Fill.SetBackgroundColor(totalBg)
                        .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right)
                        .Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorderColor(borderColor);

                    ws.Row(row).Height = 18;
                    row++;
                }

                row++;

                int headerRow = row;
                string[] headers = { "ID", "Payment Date", "Amount Paid (₨)", "Balance Before (₨)", "Balance After (₨)", "Source", "Paid To", "Remarks" };

                for (int i = 0; i < headers.Length; i++)
                {
                    var cell = ws.Cell(row, i + 1);
                    cell.Value = headers[i];
                    cell.Style.Font.SetBold(true).Font.SetFontSize(10).Font.SetFontColor(headerFg)
                        .Fill.SetBackgroundColor(headerBg)
                        .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Center)
                        .Alignment.SetVertical(XLAlignmentVerticalValues.Center)
                        .Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorderColor(borderColor);
                }
                ws.Row(row).Height = 20;
                row++;

                int dataStartRow = row;
                bool alternate = false;

                foreach (var p in payments)
                {
                    var rowBg = alternate ? altRowBg : XLColor.White;
                    object[] values =
                    {
                        p.Id,
                        p.PaymentDate.ToString("dd-MMM-yyyy hh:mm tt"),
                        p.AmountPaid,
                        p.BalanceBefore,
                        p.BalanceAfter,
                        p.PaymentSource ?? "-",
                        p.PaidTo        ?? "-",
                        p.Remarks       ?? "-"
                    };

                    for (int col = 1; col <= values.Length; col++)
                    {
                        var cell = ws.Cell(row, col);
                        cell.Value = XLCellValue.FromObject(values[col - 1]);
                        cell.Style.Fill.SetBackgroundColor(rowBg).Font.SetFontSize(10)
                            .Border.SetOutsideBorder(XLBorderStyleValues.Thin).Border.SetOutsideBorderColor(borderColor);

                        if (col == 3 || col == 4 || col == 5)
                        {
                            cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right);
                            cell.Style.NumberFormat.Format = "#,##0.00";
                        }
                        else
                            cell.Style.Alignment.SetHorizontal(XLAlignmentHorizontalValues.Left);
                    }

                    ws.Row(row).Height = 18;
                    alternate = !alternate;
                    row++;
                }

                int dataEndRow = row - 1;

                var totalLabel = ws.Range(row, 1, row, 2);
                totalLabel.Merge(); totalLabel.Value = "TOTAL PAYMENTS";
                totalLabel.Style.Font.SetBold(true).Font.SetFontSize(11).Font.SetFontColor(totalFg)
                    .Fill.SetBackgroundColor(totalBg)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right)
                    .Border.SetOutsideBorder(XLBorderStyleValues.Medium)
                    .Border.SetOutsideBorderColor(XLColor.FromHtml("#2B579A"));

                var amountCell = ws.Cell(row, 3);
                amountCell.FormulaA1 = $"=SUM(C{dataStartRow}:C{dataEndRow})";
                amountCell.Style.Font.SetBold(true).Font.SetFontSize(11).Font.SetFontColor(totalFg)
                    .Fill.SetBackgroundColor(totalBg)
                    .Alignment.SetHorizontal(XLAlignmentHorizontalValues.Right)
                    .Border.SetOutsideBorder(XLBorderStyleValues.Medium)
                    .Border.SetOutsideBorderColor(XLColor.FromHtml("#2B579A"));
                amountCell.Style.NumberFormat.Format = "#,##0.00";

                for (int col = 4; col <= totalCols; col++)
                    ws.Cell(row, col).Style.Fill.SetBackgroundColor(totalBg)
                        .Border.SetOutsideBorder(XLBorderStyleValues.Medium)
                        .Border.SetOutsideBorderColor(XLColor.FromHtml("#2B579A"));

                ws.Row(row).Height = 22;

                ws.Column(1).Width = 6;
                ws.Column(2).Width = 20;
                ws.Column(3).Width = 16;
                ws.Column(4).Width = 16;
                ws.Column(5).Width = 16;
                ws.Column(6).Width = 12;
                ws.Column(7).Width = 14;
                ws.Column(8).Width = 30;

                ws.SheetView.FreezeRows(headerRow);

                wb.SaveAs(saveDialog.FileName);
                MessageBox.Show("Payment history exported successfully!", "Success",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Export failed: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Back_Click(object sender, RoutedEventArgs e)
            => _layout.Navigate(new VendorPurchasePage(_layout, _vendorId));
    }
}