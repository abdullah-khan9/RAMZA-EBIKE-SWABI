using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Ramza_EBike_Swabi.Models;
using Ramza_EBike_Swabi.Services;

namespace Ramza_EBike_Swabi.Views.Pages
{
    public partial class AddEditIncentiveWindow : Window
    {
        private readonly VendorService _vendorService = new();
        private readonly VendorIncentiveService _incentiveService = new();
        private readonly VendorCashService _vendorCashService = new();

        private List<Vendor> _vendors = new();
        private VendorIncentive? _editingIncentive;  // null = add mode

        // ===========================
        // CONSTRUCTORS
        // ===========================
        public AddEditIncentiveWindow()
        {
            InitializeComponent();

            // ✅ FIX: Wire radio button Checked events AFTER InitializeComponent()
            // so that all named controls (pnlVendorCashInfo, txtDestHint, etc.)
            // are fully created before the events can fire. Wiring them in XAML
            // caused rbCash (IsChecked="True") to fire Destination_Changed during
            // InitializeComponent before the other controls existed, crashing the app.
            rbCash.Checked += Destination_Changed;
            rbAccount.Checked += Destination_Changed;
            rbVendorCash.Checked += Destination_Changed;
            rbGift.Checked += Destination_Changed;

            dpDate.SelectedDate = DateTime.Today;
            txtTime.Text = DateTime.Now.ToString("HH:mm");
            Loaded += OnLoaded;
            UpdateDestHint();
        }

        // Edit mode — pre-fill form
        public AddEditIncentiveWindow(VendorIncentive incentive) : this()
        {
            _editingIncentive = incentive;
            FormTitle.Text = "Edit Vendor Incentive";
            btnSave.Content = "Save Changes";
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                _vendors = await _vendorService.GetVendorsAsync();
                cmbVendor.ItemsSource = _vendors;

                if (_editingIncentive != null)
                    PreFillForm(_editingIncentive);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                Close();
            }
        }

        private void PreFillForm(VendorIncentive incentive)
        {
            // Select vendor
            cmbVendor.SelectedItem = _vendors.Find(v => v.Id == incentive.VendorId);
            txtIncentiveName.Text = incentive.IncentiveName;
            txtAmount.Text = incentive.Amount.ToString("N2");
            txtRemarks.Text = incentive.Remarks ?? string.Empty;
            dpDate.SelectedDate = incentive.IncentiveDate.Date;
            txtTime.Text = incentive.IncentiveDate.ToString("HH:mm");

            switch (incentive.Destination)
            {
                case IncentiveDestination.Cash: rbCash.IsChecked = true; break;
                case IncentiveDestination.Account: rbAccount.IsChecked = true; break;
                case IncentiveDestination.VendorCash: rbVendorCash.IsChecked = true; break;
                case IncentiveDestination.Gift: rbGift.IsChecked = true; break;
            }
        }

        // ===========================
        // VENDOR CHANGED
        // ===========================
        private async void CmbVendor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cmbVendor.SelectedItem is not Vendor vendor) return;

            // ✅ FIX: Guard against null in case this fires before controls are ready
            if (pnlVendorCashInfo == null || rbVendorCash == null) return;

            try
            {
                decimal cash = await _vendorCashService.GetVendorCashBalanceAsync(vendor.Id);
                txtVendorCashBalance.Text = $"PKR {cash:N2}";
                pnlVendorCashInfo.Visibility = rbVendorCash.IsChecked == true
                    ? Visibility.Visible : Visibility.Collapsed;
            }
            catch { }
        }

        // ===========================
        // DESTINATION CHANGED
        // ===========================
        private void Destination_Changed(object sender, RoutedEventArgs e)
        {
            UpdateDestHint();

            // ✅ FIX: Guard against null — this event can fire before all controls exist
            if (pnlVendorCashInfo == null || rbVendorCash == null) return;

            pnlVendorCashInfo.Visibility = rbVendorCash.IsChecked == true
                ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateDestHint()
        {
            if (txtDestHint == null) return;

            if (rbCash?.IsChecked == true)
                txtDestHint.Text = "The incentive amount will be added to Cash Balance and recorded as a Cash Deposit transaction.";
            else if (rbAccount?.IsChecked == true)
                txtDestHint.Text = "The incentive amount will be added to Account/Bank Balance and recorded as a Bank Deposit transaction.";
            else if (rbVendorCash?.IsChecked == true)
                txtDestHint.Text = "The incentive amount will be added to the selected vendor's Cash Balance (pre-payment wallet).";
            else
                txtDestHint.Text = "Gift — no amount is added to any balance. Only a record is kept.";
        }

        // ===========================
        // SAVE
        // ===========================
        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (cmbVendor.SelectedItem is not Vendor vendor)
                {
                    MessageBox.Show("Please select a vendor.", "Validation",
                        MessageBoxButton.OK, MessageBoxImage.Warning); return;
                }

                if (string.IsNullOrWhiteSpace(txtIncentiveName.Text))
                {
                    MessageBox.Show("Please enter an incentive name.", "Validation",
                        MessageBoxButton.OK, MessageBoxImage.Warning); return;
                }

                if (!decimal.TryParse(txtAmount.Text, out decimal amount) || amount <= 0)
                {
                    MessageBox.Show("Please enter a valid amount.", "Validation",
                        MessageBoxButton.OK, MessageBoxImage.Warning); return;
                }

                if (dpDate.SelectedDate == null)
                {
                    MessageBox.Show("Please select a date.", "Validation",
                        MessageBoxButton.OK, MessageBoxImage.Warning); return;
                }

                DateTime txnDate = dpDate.SelectedDate.Value.Date;
                if (TimeSpan.TryParse(txtTime.Text, out TimeSpan time))
                    txnDate = txnDate.Add(time);

                var destination = rbCash?.IsChecked == true ? IncentiveDestination.Cash
                                : rbAccount?.IsChecked == true ? IncentiveDestination.Account
                                : rbVendorCash?.IsChecked == true ? IncentiveDestination.VendorCash
                                                                  : IncentiveDestination.Gift;

                bool success;
                string message;

                if (_editingIncentive == null)
                {
                    (success, message) = await _incentiveService.AddIncentiveAsync(
                        vendor.Id, vendor.VendorName,
                        txtIncentiveName.Text.Trim(),
                        amount, destination, txnDate,
                        txtRemarks.Text?.Trim());
                }
                else
                {
                    (success, message) = await _incentiveService.UpdateIncentiveAsync(
                        _editingIncentive.Id,
                        txtIncentiveName.Text.Trim(),
                        amount, destination, txnDate,
                        txtRemarks.Text?.Trim());
                }

                MessageBox.Show(message,
                    success ? "Success" : "Error",
                    MessageBoxButton.OK,
                    success ? MessageBoxImage.Information : MessageBoxImage.Error);

                if (success) DialogResult = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error:\n{ex.Message}\n\n{ex.InnerException?.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}