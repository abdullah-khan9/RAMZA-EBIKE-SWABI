using Ramza_EBike_Swabi.Models;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Ramza_EBike_Swabi.Views.Windows
{
    public partial class PayInstalmentWindow : Window
    {
        public decimal CashAmount { get; private set; }
        public decimal AccountAmount { get; private set; }
        public string? Remarks { get; private set; }

        private readonly decimal _dueAmount;

        public PayInstalmentWindow(InvoiceInstalment inst)
        {
            InitializeComponent();
            // ✅ Remaining due on THIS instalment (Amount - already paid), not the full
            // original amount — otherwise a follow-up payment on a partially-paid
            // instalment would default to (and allow) paying the full amount again.
            _dueAmount = Math.Max(0, inst.Amount - inst.PaidAmount);
            TitleText.Text = $"Instalment #{inst.InstalmentNumber}  |  Due: PKR {_dueAmount:N2}  |  Date: {inst.DueDate:dd-MMM-yyyy}";
            txtCash.Text = _dueAmount.ToString("N2");
            txtAccount.Text = "0";
            UpdateTotal();
        }

        private void Recalculate(object sender, TextChangedEventArgs e) => UpdateTotal();

        private void UpdateTotal()
        {
            decimal cash = decimal.TryParse(txtCash?.Text, out var c) ? c : 0;
            decimal account = decimal.TryParse(txtAccount?.Text, out var a) ? a : 0;
            if (txtTotalDisplay != null)
                txtTotalDisplay.Text = $"PKR {(cash + account):N2}";
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            decimal cash = decimal.TryParse(txtCash.Text, out var c) ? c : 0;
            decimal account = decimal.TryParse(txtAccount.Text, out var a) ? a : 0;
            decimal total = cash + account;

            if (total <= 0)
            {
                MessageBox.Show("Cash ya Account mein se kum az kum ek amount enter karein.",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            // ✅ Don't let a single instalment payment exceed what's actually still due on it
            if (total > _dueAmount)
            {
                MessageBox.Show(
                    $"Amount (PKR {total:N2}) is instalment ki baqi raqam (PKR {_dueAmount:N2}) se zyada hai.",
                    "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CashAmount = cash;
            AccountAmount = account;
            Remarks = txtRemarks.Text?.Trim();
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e) => Close();
    }
}