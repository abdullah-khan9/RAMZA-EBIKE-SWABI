using Ramza_EBike_Swabi.Models;
using Ramza_EBike_Swabi.Services;
using Ramza_EBike_Swabi.Services.Pdf;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

using MediaColor = System.Windows.Media.Color;

namespace Ramza_EBike_Swabi.Views.Pages
{
    public partial class VendorBillDetailPage : Page
    {
        private readonly VendorService _service = new();
        private readonly MainLayout _layout;
        private VendorBill? _bill;

        public VendorBillDetailPage(MainLayout layout, int billId)
        {
            InitializeComponent();
            _layout = layout;
            LoadBill(billId);
        }

        private async void LoadBill(int billId)
        {
            _bill = await _service.GetBillByIdAsync(billId);
            if (_bill == null) { MessageBox.Show("Bill not found!"); return; }

            // Header
            BillIdText.Text = _bill.Id.ToString();
            PrintDateText.Text = DateTime.Now.ToString("dd MMM yyyy hh:mm tt");

            // Vendor info
            VendorName.Text = _bill.Vendor?.VendorName ?? "Unknown Vendor";
            VendorId.Text = $"Vendor ID: {_bill.VendorId}";
            BillDate.Text = _bill.BillDate.ToString("dd MMM yyyy");
            DocumentNumberText.Text = _bill.Items?.Count > 0
                ? (_bill.Items[0].DocumentNumber ?? "-") : "-";

            // Top info panel
            PreviousBalance.Text = $"PKR {_bill.PreviousBalance:N2}";
            AmountPaid.Text = $"PKR {_bill.AmountPaid:N2}";

            // Commission / Tax
            TotalCommissionText.Text = $"PKR {_bill.TotalCommission:N2}";
            TotalTaxText.Text = $"PKR {_bill.TotalTaxPaid:N2}";

            // ── Full financial summary ──────────────────────────────
            decimal netBill = _bill.PreviousBalance + _bill.TotalAmount;

            PreviousBalanceSummary.Text = $"PKR {_bill.PreviousBalance:N2}";
            TotalAmount.Text = $"PKR {_bill.TotalAmount:N2}";
            NetBillText.Text = $"PKR {netBill:N2}";
            AmountPaidSummary.Text = $"PKR {_bill.AmountPaid:N2}";
            RemainingBalance.Text = $"PKR {_bill.RemainingBalance:N2}";

            string paidVia = _bill.PaymentSource switch
            {
                "Cash" => "Cash Balance",
                "Account" => "Account / Bank",
                "VendorCash" => "Vendor Cash",
                "None" => "Not Specified",
                _ => _bill.PaymentSource ?? "—"
            };
            PaidViaSummary.Text = paidVia;
            PaymentMethodText.Text = paidVia;

            // Remaining balance colour (green = paid, red = outstanding)
            bool fullyPaid = _bill.RemainingBalance <= 0;
            var greenBrush = new SolidColorBrush(MediaColor.FromRgb(0x1B, 0x8A, 0x3D));
            var redBrush = new SolidColorBrush(MediaColor.FromRgb(0xB0, 0x00, 0x20));

            RemainingBalance.Foreground = fullyPaid ? greenBrush : redBrush;
            StatusBadge.Background = fullyPaid ? greenBrush : redBrush;
            StatusText.Text = fullyPaid ? "PAID" : "OUTSTANDING";

            // Remarks
            if (!string.IsNullOrWhiteSpace(_bill.Remarks))
            {
                RemarksText.Text = _bill.Remarks;
                RemarksBorder.Visibility = Visibility.Visible;
            }
            else
            {
                RemarksBorder.Visibility = Visibility.Collapsed;
            }

            // Items
            ItemsGrid.ItemsSource = _bill.Items
                ?? new System.Collections.Generic.List<VendorBillItem>();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
        {
            if (_bill != null)
                _layout.Navigate(new VendorPurchasePage(_layout, _bill.VendorId));
        }

        private void Print_Click(object sender, RoutedEventArgs e) => ExportToPdf();

        public void ExportToPdf()
        {
            if (_bill == null) { MessageBox.Show("Bill not loaded"); return; }
            try
            {
                VendorBillPdfService.Generate(_bill);
                MessageBox.Show("PDF generated successfully!",
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error generating PDF:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}