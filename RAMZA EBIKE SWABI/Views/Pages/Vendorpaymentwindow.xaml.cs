using Ramza_EBike_Swabi.Models;
using Ramza_EBike_Swabi.Services;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Ramza_EBike_Swabi.Views.Windows
{
    public partial class VendorPaymentWindow : Window
    {
        private readonly int _vendorId;
        private readonly string _vendorName;
        private readonly decimal _remainingBalance;

        private readonly AccountService _accountService = new();
        private readonly VendorCashService _vendorCashService = new();
        private readonly VendorService _vendorService = new();

        private decimal _cashBalance;
        private decimal _bankBalance;
        private decimal _vendorCash;

        public bool PaymentSubmitted { get; private set; } = false;

        public VendorPaymentWindow(int vendorId, string vendorName, decimal remainingBalance)
        {
            InitializeComponent();
            _vendorId = vendorId;
            _vendorName = vendorName;
            _remainingBalance = remainingBalance;

            VendorNameText.Text = $"Vendor: {vendorName}";
            RemainingBalanceDisplay.Text = $"PKR {remainingBalance:N2}";
            BalanceAfterDisplay.Text = $"PKR {remainingBalance:N2}";

            LoadBalancesAsync();
        }

        private async void LoadBalancesAsync()
        {
            try
            {
                var balance = await _accountService.GetBalanceAsync();
                _cashBalance = balance.CashBalance;
                _bankBalance = balance.BankBalance;
                _vendorCash = await _vendorCashService.GetVendorCashBalanceAsync(_vendorId);

                UpdateAvailableHint();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading balances: {ex.Message}");
            }
        }

        private void PaymentSourceCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => UpdateAvailableHint();

        private void UpdateAvailableHint()
        {
            var item = (PaymentSourceCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Cash";
            decimal available = item switch
            {
                "Account" => _bankBalance,
                "VendorCash" => _vendorCash,
                _ => _cashBalance
            };
            if (AvailableBalanceHint != null)
                AvailableBalanceHint.Text = $"Available ({item}): PKR {available:N2}";
        }

        private void AmountBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (decimal.TryParse(AmountBox.Text, out decimal amount))
            {
                decimal after = _remainingBalance - amount;
                BalanceAfterDisplay.Text = $"PKR {after:N2}";
                BalanceAfterDisplay.Foreground = after < 0
                    ? System.Windows.Media.Brushes.Red
                    : System.Windows.Media.Brushes.Green;
            }
            else
            {
                BalanceAfterDisplay.Text = $"PKR {_remainingBalance:N2}";
            }
        }

        private async void Submit_Click(object sender, RoutedEventArgs e)
        {
            // ── Validate amount ──────────────────────────────────
            if (!decimal.TryParse(AmountBox.Text.Trim(), out decimal amount) || amount <= 0)
            {
                MessageBox.Show("Please enter a valid payment amount.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (string.IsNullOrWhiteSpace(PaidToBox.Text))
            {
                MessageBox.Show("Please enter the person this payment was made to.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (PaymentDatePicker.SelectedDate == null)
            {
                MessageBox.Show("Please select a payment date.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string source = (PaymentSourceCombo.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Cash";

            // ── Validate source has enough funds ─────────────────
            decimal available = source switch
            {
                "Account" => _bankBalance,
                "VendorCash" => _vendorCash,
                _ => _cashBalance
            };

            if (amount > available)
            {
                MessageBox.Show(
                    $"Insufficient {source} balance.\nAvailable: PKR {available:N2}\nRequested: PKR {amount:N2}",
                    "Insufficient Funds", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // ── Confirm ──────────────────────────────────────────
            decimal balanceAfter = _remainingBalance - amount;
            var confirm = MessageBox.Show(
                $"Confirm payment to vendor:\n\n" +
                $"  Vendor      : {_vendorName}\n" +
                $"  Amount      : PKR {amount:N2}\n" +
                $"  Source      : {source}\n" +
                $"  Paid To     : {PaidToBox.Text.Trim()}\n" +
                $"  Balance After: PKR {balanceAfter:N2}",
                "Confirm Payment", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                IsEnabled = false;

                await _vendorService.RecordVendorPaymentAsync(new VendorPaymentRecord
                {
                    VendorId = _vendorId,
                    VendorName = _vendorName,
                    BalanceBefore = _remainingBalance,
                    AmountPaid = amount,
                    BalanceAfter = balanceAfter,
                    PaymentSource = source,
                    PaidTo = PaidToBox.Text.Trim(),
                    Remarks = RemarksBox.Text.Trim(),
                    PaymentDate = PaymentDatePicker.SelectedDate!.Value
                }, source, _accountService, _vendorCashService);

                PaymentSubmitted = true;
                MessageBox.Show(
                    $"Payment of PKR {amount:N2} recorded successfully.\nNew remaining balance: PKR {balanceAfter:N2}",
                    "Payment Recorded", MessageBoxButton.OK, MessageBoxImage.Information);

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                IsEnabled = true;
                MessageBox.Show($"Error recording payment: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}