using Ramza_EBike_Swabi.Models;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Ramza_EBike_Swabi.Views.Windows
{
    public partial class EditVendorPaymentWindow : Window
    {
        private readonly VendorPaymentRecord _original;

        /// <summary>Populated on successful save — caller reads this to update the DB.</summary>
        public VendorPaymentRecord UpdatedRecord { get; private set; } = null!;

        public EditVendorPaymentWindow(VendorPaymentRecord record)
        {
            InitializeComponent();
            _original = record;

            SubtitleText.Text = $"Payment ID #{record.Id}  |  Vendor: {record.VendorName}";

            // Pre-fill fields
            AmountBox.Text = record.AmountPaid.ToString("F2");
            PaidToBox.Text = record.PaidTo ?? "";
            RemarksBox.Text = record.Remarks ?? "";
            PaymentDatePicker.SelectedDate = record.PaymentDate.Date;

            // Select correct ComboBox item
            string source = record.PaymentSource ?? "Cash";
            foreach (ComboBoxItem item in PaymentSourceCombo.Items)
            {
                if (item.Content?.ToString() == source)
                {
                    PaymentSourceCombo.SelectedItem = item;
                    break;
                }
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            // ── Validate amount ──────────────────────────────────────
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

            // Build the updated record (preserve Id, VendorId, VendorName, BalanceBefore)
            UpdatedRecord = new VendorPaymentRecord
            {
                Id = _original.Id,
                VendorId = _original.VendorId,
                VendorName = _original.VendorName,
                BalanceBefore = _original.BalanceBefore,    // unchanged snapshot
                AmountPaid = amount,
                BalanceAfter = _original.BalanceBefore - amount,  // recomputed
                PaymentSource = source,
                PaidTo = PaidToBox.Text.Trim(),
                Remarks = RemarksBox.Text.Trim(),
                PaymentDate = PaymentDatePicker.SelectedDate!.Value
            };

            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}