// AddVendorCashWindow.xaml.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Ramza_EBike_Swabi.Models;
using Ramza_EBike_Swabi.Services;

namespace Ramza_EBike_Swabi.Views.Pages
{
    public partial class AddVendorCashWindow : Window
    {
        private readonly VendorService _vendorService;
        private readonly VendorCashService _vendorCashService;
        private readonly AccountService _accountService;

        private System.Collections.Generic.List<Vendor> _vendors = new();

        private decimal _accountCashBalance = 0m;
        private decimal _accountBankBalance = 0m;
        private decimal _vendorRemaining = 0m;
        private decimal _vendorCashBalance = 0m;

        private readonly int? _preSelectedVendorId;

        // Parameterless ctor — Dashboard use karta hai
        public AddVendorCashWindow() : this(null) { }

        // VendorCashPage se vendor pre-select ke saath
        public AddVendorCashWindow(int? preSelectedVendorId)
        {
            InitializeComponent();

            _preSelectedVendorId = preSelectedVendorId;
            _vendorService = new VendorService();
            _vendorCashService = new VendorCashService();
            _accountService = new AccountService();

            dpDate.SelectedDate = DateTime.Today;
            txtTime.Text = DateTime.Now.ToString("HH:mm");

            Loaded += async (_, __) =>
            {
                try
                {
                    await LoadDataAsync();
                    rbCash.Checked += Source_Changed;
                    rbAccount.Checked += Source_Changed;
                    UpdateSourceHint();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error loading window:\n{ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };
        }

        private async Task LoadDataAsync()
        {
            try
            {
                _vendors = await _vendorService.GetVendorsAsync()
                           ?? new System.Collections.Generic.List<Vendor>();

                cmbVendor.ItemsSource = _vendors;

                // Pre-select vendor if provided
                if (_preSelectedVendorId.HasValue)
                {
                    var target = _vendors.FirstOrDefault(v => v.Id == _preSelectedVendorId.Value);
                    if (target != null) cmbVendor.SelectedItem = target;
                }

                var balance = await _accountService.GetBalanceAsync();
                if (balance != null)
                {
                    _accountCashBalance = balance.CashBalance;
                    _accountBankBalance = balance.BankBalance;
                }

                UpdateSourceHint();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading data:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─── Vendor selection ───────────────────────────────────────
        private async void CmbVendor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (cmbVendor.SelectedItem is not Vendor vendor) return;

                // ✅ Use GetTrueRemainingBalanceAsync for accurate remaining balance
                _vendorRemaining = await _vendorService.GetTrueRemainingBalanceAsync(vendor.Id);

                _vendorCashBalance = await _vendorCashService.GetVendorCashBalanceAsync(vendor.Id);

                txtVendorRemaining.Text = $"PKR {_vendorRemaining:N2}";
                txtVendorCash.Text = $"PKR {_vendorCashBalance:N2}";

                pnlVendorInfo.Visibility = Visibility.Visible;
                UpdateAfterPaymentPreview();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading vendor info:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ─── Amount changed ─────────────────────────────────────────
        private void TxtAmount_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateAfterPaymentPreview();

            if (!decimal.TryParse(txtAmount.Text, out decimal amount) || amount <= 0)
            {
                txtAmountHint.Text = string.Empty;
                return;
            }

            // Simple hint: full amount goes to vendor cash
            txtAmountHint.Text = $"PKR {amount:N2} will be added to vendor's cash balance.";
        }

        // ─── Preview: after adding, what will vendor cash be? ───────
        private void UpdateAfterPaymentPreview()
        {
            if (txtAfterPayment == null) return;

            if (!decimal.TryParse(txtAmount.Text, out decimal amount) || amount <= 0)
            {
                txtAfterPayment.Text = "-";
                return;
            }

            decimal newVendorCash = _vendorCashBalance + amount;

            // Remaining bill stays unchanged (we are NOT applying to bill here)
            txtAfterPayment.Text =
                $"Vendor Cash: PKR {newVendorCash:N2}  |  Bill Remaining: PKR {_vendorRemaining:N2} (unchanged)";
        }

        // ─── Source radio button ─────────────────────────────────────
        private void Source_Changed(object sender, RoutedEventArgs e)
        {
            if (!IsLoaded) return;
            UpdateSourceHint();
        }

        private void UpdateSourceHint()
        {
            if (txtAvailableHint == null || rbCash == null || rbAccount == null) return;

            txtAvailableHint.Text = rbCash.IsChecked == true
                ? $"Available Cash Balance: PKR {_accountCashBalance:N2}"
                : $"Available Account Balance: PKR {_accountBankBalance:N2}";
        }

        // ─── Save ────────────────────────────────────────────────────
        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (cmbVendor.SelectedItem is not Vendor vendor)
                { MessageBox.Show("Please select a vendor."); return; }

                if (string.IsNullOrWhiteSpace(txtByWhom.Text))
                { MessageBox.Show("Please enter By Whom."); return; }

                if (!decimal.TryParse(txtAmount.Text, out decimal amount) || amount <= 0)
                { MessageBox.Show("Please enter a valid amount."); return; }

                if (dpDate.SelectedDate == null)
                { MessageBox.Show("Please select a date."); return; }

                DateTime txnDate = dpDate.SelectedDate.Value;
                if (TimeSpan.TryParse(txtTime.Text, out TimeSpan time))
                    txnDate = txnDate.Add(time);

                string source = rbCash.IsChecked == true ? "Cash" : "Account";

                var (success, message) = await _vendorCashService.AddCashToVendorAsync(
                    vendor.Id,
                    vendor.VendorName,
                    txtByWhom.Text.Trim(),
                    amount,
                    source,
                    txnDate,
                    txtRemarks.Text?.Trim());

                MessageBox.Show(message, success ? "Success" : "Error",
                    MessageBoxButton.OK,
                    success ? MessageBoxImage.Information : MessageBoxImage.Error);

                if (success) DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}