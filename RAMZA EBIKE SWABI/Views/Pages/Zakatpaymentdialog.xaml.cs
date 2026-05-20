// Views/Pages/ZakatPaymentDialog.xaml.cs
using Ramza_EBike_Swabi.Models;
using Ramza_EBike_Swabi.Services;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Ramza_EBike_Swabi.Views.Pages
{
    public partial class ZakatPaymentDialog : Window
    {
        private readonly ZakatRecord _record;
        private readonly ZakatService _zakatService;
        private readonly AccountService _accountService;

        private bool _allowVoluntary = false;

        /// <summary>
        /// Set this BEFORE calling ShowDialog().
        /// When true the payment is treated as voluntary (Sadaqah) —
        /// TotalPaid / RemainingDue on the ZakatRecord will NOT change.
        /// </summary>
        public bool AllowVoluntary
        {
            get => _allowVoluntary;
            set
            {
                _allowVoluntary = value;
                // If called after window is loaded, update banner immediately
                if (IsLoaded)
                    ApplyVoluntaryBanner();
            }
        }

        // ── Constructor ───────────────────────────────────────────
        public ZakatPaymentDialog(
            ZakatRecord record,
            ZakatService zakatService,
            AccountService accountService)
        {
            InitializeComponent();

            _record = record;
            _zakatService = zakatService;
            _accountService = accountService;

            // Summary strip
            lblTotalDue.Text = $"PKR {record.TotalZakatDue:N0}";
            lblPaid.Text = $"PKR {record.TotalPaid:N0}";
            lblRemaining.Text = $"PKR {record.RemainingDue:N0}";

            // Default date
            dpPaymentDate.SelectedDate = DateTime.Today;

            // Pre-fill amount with remaining due
            decimal suggest = record.RemainingDue > 0 ? record.RemainingDue : 0m;
            txtAmount.Text = suggest > 0 ? suggest.ToString("F0") : "";
        }

        // ── Show voluntary banner after render ────────────────────
        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            ApplyVoluntaryBanner();
        }

        private void ApplyVoluntaryBanner()
        {
            if (VoluntaryBanner == null) return;
            VoluntaryBanner.Visibility = _allowVoluntary
                ? Visibility.Visible
                : Visibility.Collapsed;

            if (_allowVoluntary && btnConfirm != null)
                btnConfirm.Content = "Record Charity Payment";
        }

        // ── Confirm ───────────────────────────────────────────────
        private async void Confirm_Click(object sender, RoutedEventArgs e)
        {
            HideError();

            // ── Validate ─────────────────────────────────────────
            if (string.IsNullOrWhiteSpace(txtRecipient.Text))
            { ShowError("Recipient name is required."); return; }

            if (!decimal.TryParse(txtAmount.Text, out decimal amount) || amount <= 0)
            { ShowError("Please enter a valid amount greater than zero."); return; }

            if (dpPaymentDate.SelectedDate == null)
            { ShowError("Please select a payment date."); return; }

            if (string.IsNullOrWhiteSpace(txtPaidBy.Text))
            { ShowError("Please enter the name of the person who paid."); return; }

            // Source from ComboBox
            string source = (cmbSource.SelectedItem as ComboBoxItem)
                            ?.Content?.ToString() ?? "Cash";

            // ── Build payment ─────────────────────────────────────
            var payment = new ZakatPayment
            {
                ZakatRecordId = _record.Id,
                RecipientName = txtRecipient.Text.Trim(),
                Amount = amount,
                PaymentDate = dpPaymentDate.SelectedDate.Value,
                PaidByName = txtPaidBy.Text.Trim(),
                PaymentSource = source,
                Remarks = txtRemarks.Text.Trim()
            };

            // Prevent double-click
            btnConfirm.IsEnabled = false;

            try
            {
                var (ok, msg) = await _zakatService.AddPaymentAsync(
                    payment,
                    _accountService,
                    allowVoluntary: _allowVoluntary);

                if (ok)
                {
                    // Show success briefly, then close
                    MessageBox.Show(msg, "Payment Recorded",
                        MessageBoxButton.OK, MessageBoxImage.Information);
                    DialogResult = true;
                    Close();
                }
                else
                {
                    ShowError(msg);
                    btnConfirm.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                ShowError($"Unexpected error:\n{ex.Message}");
                btnConfirm.IsEnabled = true;
            }
        }

        // ── Cancel ────────────────────────────────────────────────
        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        // ── Helpers ───────────────────────────────────────────────
        private void ShowError(string message)
        {
            txtError.Text = message;
            ErrorBanner.Visibility = Visibility.Visible;
        }

        private void HideError()
            => ErrorBanner.Visibility = Visibility.Collapsed;
    }
}