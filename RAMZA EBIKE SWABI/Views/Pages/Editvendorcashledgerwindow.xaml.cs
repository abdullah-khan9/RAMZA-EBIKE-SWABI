using System;
using System.Windows;

namespace Ramza_EBike_Swabi.Views.Pages
{
    public partial class EditVendorCashLedgerWindow : Window
    {
        // Results read by the caller after ShowDialog() == true
        public string EditedByWhom { get; private set; } = string.Empty;
        public string? EditedRemarks { get; private set; }
        public decimal EditedAmount { get; private set; }
        public string EditedSource { get; private set; } = string.Empty;
        public DateTime EditedDate { get; private set; }
        public string TransactionType { get; private set; } = string.Empty;
        public int VendorId { get; private set; }
        public decimal OriginalAmount { get; private set; }
        public string OriginalSource { get; private set; } = string.Empty;

        public EditVendorCashLedgerWindow(VendorCashLedgerRow row)
        {
            InitializeComponent();

            // Store original values for comparison
            OriginalAmount = row.Amount;
            OriginalSource = row.Source;
            VendorId = row.VendorId;
            TransactionType = row.TypeDisplay;

            // Pre-fill fields
            txtVendorRO.Text = row.VendorName;
            txtTypeRO.Text = row.TypeDisplay;
            txtByWhom.Text = row.ByWhom;
            txtAmount.Text = row.Amount.ToString("N2");
            txtRemarks.Text = row.Remarks ?? string.Empty;

            dpDate.SelectedDate = row.TransactionDate.Date;
            txtTime.Text = row.TransactionDate.ToString("HH:mm");

            // Set source
            string source = row.Source?.ToLower() ?? "cash";
            if (source.Contains("account") || source.Contains("bank"))
                cmbSource.SelectedIndex = 1; // Account
            else
                cmbSource.SelectedIndex = 0; // Cash

            // Disable source selection for "Applied" type transactions
            if (row.RawType == Models.VendorCashType.Applied)
            {
                cmbSource.IsEnabled = false;
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtByWhom.Text))
            {
                MessageBox.Show("By Whom cannot be empty.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!decimal.TryParse(txtAmount.Text, out decimal amount) || amount <= 0)
            {
                MessageBox.Show("Enter a valid amount.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (dpDate.SelectedDate == null)
            {
                MessageBox.Show("Select a date.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (cmbSource.SelectedItem == null)
            {
                MessageBox.Show("Select a payment source.", "Validation",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            DateTime date = dpDate.SelectedDate.Value;
            if (TimeSpan.TryParse(txtTime.Text, out TimeSpan time))
                date = date.Add(time);

            EditedByWhom = txtByWhom.Text.Trim();
            EditedRemarks = txtRemarks.Text?.Trim();
            EditedAmount = amount;
            EditedDate = date;
            EditedSource = ((System.Windows.Controls.ComboBoxItem)cmbSource.SelectedItem).Tag.ToString() ?? "Cash";

            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}