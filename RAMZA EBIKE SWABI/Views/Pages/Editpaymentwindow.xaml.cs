// EditPaymentWindow.xaml.cs
using System;
using System.Windows;
using Ramza_EBike_Swabi.Views.Pages;

namespace Ramza_EBike_Swabi.Views.Pages
{
    public partial class EditPaymentWindow : Window
    {
        // Output properties — PaymentHistoryWindow reads these
        public decimal NewCashAmount { get; private set; }
        public decimal NewAccountAmount { get; private set; }
        public string NewReceivedBy { get; private set; } = string.Empty;
        public DateTime NewDate { get; private set; }

        private readonly PaymentHistoryRow _row;
        private readonly Ramza_EBike_Swabi.Models.InvoiceInstalment? _instalment;

        // ✅ instalment: pass the linked InvoiceInstalment when this payment came from an
        // instalment plan — the window then labels itself accordingly and caps the entry at
        // what's still owed on THAT instalment (not just the invoice's overall NetBill).
        public EditPaymentWindow(PaymentHistoryRow row, Ramza_EBike_Swabi.Models.InvoiceInstalment? instalment = null)
        {
            InitializeComponent();
            _row = row;
            _instalment = instalment;

            // Pre-fill with existing values
            txtCashAmount.Text = row.AmountPaidCash > 0
                ? row.AmountPaidCash.ToString("N2") : string.Empty;
            txtAccountAmount.Text = row.AmountPaidAccount > 0
                ? row.AmountPaidAccount.ToString("N2") : string.Empty;
            txtReceivedBy.Text = row.ReceivedBy;
            dpDate.SelectedDate = row.PaymentDate;

            if (_instalment != null)
            {
                Title = $"Edit Instalment #{_instalment.InstalmentNumber} Payment";
                txtHeaderTitle.Text = $"Edit Instalment #{_instalment.InstalmentNumber} Payment";
                txtSubtitle.Text = $"📅 Instalment Amount: PKR {_instalment.Amount:N2}";
                txtSubtitle.Visibility = Visibility.Visible;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            decimal cash = decimal.TryParse(txtCashAmount.Text, out var c) ? c : 0;
            decimal account = decimal.TryParse(txtAccountAmount.Text, out var a) ? a : 0;

            if (cash + account <= 0)
            {
                MessageBox.Show("Please enter at least one amount (Cash or Account).",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtReceivedBy.Text))
            {
                MessageBox.Show("Please enter Received By.",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // ✅ For instalment-linked payments, cap the edit at what's still owed on THAT
            // instalment specifically (not just the invoice's overall NetBill).
            if (_instalment != null)
            {
                decimal otherAlreadyPaidOnInstalment = Math.Max(0, _instalment.PaidAmount - _row.AmountPaid);
                decimal maxAllowed = _instalment.Amount - otherAlreadyPaidOnInstalment;
                if (cash + account > maxAllowed)
                {
                    MessageBox.Show(
                        $"Yeh amount instalment #{_instalment.InstalmentNumber} ki had (PKR {maxAllowed:N2}) se zyada hai.",
                        "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            NewCashAmount = cash;
            NewAccountAmount = account;
            NewReceivedBy = txtReceivedBy.Text.Trim();
            NewDate = dpDate.SelectedDate ?? DateTime.Today;

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}