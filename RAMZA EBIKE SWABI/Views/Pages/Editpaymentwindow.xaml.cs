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

        public EditPaymentWindow(PaymentHistoryRow row)
        {
            InitializeComponent();

            // Pre-fill with existing values
            txtCashAmount.Text = row.AmountPaidCash > 0
                ? row.AmountPaidCash.ToString("N2") : string.Empty;
            txtAccountAmount.Text = row.AmountPaidAccount > 0
                ? row.AmountPaidAccount.ToString("N2") : string.Empty;
            txtReceivedBy.Text = row.ReceivedBy;
            dpDate.SelectedDate = row.PaymentDate;
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