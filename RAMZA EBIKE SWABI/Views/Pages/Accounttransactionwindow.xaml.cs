using System;
using System.Windows;
using System.Windows.Media;
using Ramza_EBike_Swabi.Models;
using Ramza_EBike_Swabi.Services;

namespace Ramza_EBike_Swabi.Views.Pages
{
    public partial class AccountTransactionWindow : Window
    {
        private readonly AccountService _accountService = new();

        public AccountTransactionWindow()
        {
            InitializeComponent();
            dpDate.SelectedDate = DateTime.Today;
            txtTime.Text = DateTime.Now.ToString("HH:mm");
            Loaded += (_, __) => HighlightSelected();
        }

        private void Option_Checked(object sender, RoutedEventArgs e)
            => HighlightSelected();

        private void HighlightSelected()
        {
            try
            {
                var active = new SolidColorBrush(Color.FromRgb(0x1B, 0x40, 0x79));
                var inactive = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC));

                BorderConvert.BorderBrush = rbConvertCashToAccount.IsChecked == true ? active : inactive;
                BorderDepositCash.BorderBrush = rbDepositCash.IsChecked == true ? active : inactive;
                BorderDepositAccount.BorderBrush = rbDepositAccount.IsChecked == true ? active : inactive;
                BorderWithdrawAccount.BorderBrush = rbWithdrawAccount.IsChecked == true ? active : inactive;
                BorderWithdrawCash.BorderBrush = rbWithdrawCash.IsChecked == true ? active : inactive;
                BorderWithdrawFromAccount.BorderBrush = rbWithdrawFromAccount.IsChecked == true ? active : inactive; // ✅ NEW
            }
            catch { }
        }

        private async void Save_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(txtByWhom.Text))
                {
                    MessageBox.Show("Please enter who performed this transaction.",
                        "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!decimal.TryParse(txtAmount.Text, out decimal amount) || amount <= 0)
                {
                    MessageBox.Show("Please enter a valid amount greater than zero.",
                        "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (dpDate.SelectedDate == null)
                {
                    MessageBox.Show("Please select a date.",
                        "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                DateTime txnDate = dpDate.SelectedDate.Value.Date;
                if (TimeSpan.TryParse(txtTime.Text, out TimeSpan time))
                    txnDate = txnDate.Add(time);

                // ✅ rbWithdrawFromAccount → WithdrawFromAccount (sirf BankBalance kam hoga)
                TransactionType type;
                if (rbConvertCashToAccount.IsChecked == true) type = TransactionType.ConvertCashToAccount;
                else if (rbDepositCash.IsChecked == true) type = TransactionType.CashDeposit;
                else if (rbDepositAccount.IsChecked == true) type = TransactionType.DepositToAccount;
                else if (rbWithdrawAccount.IsChecked == true) type = TransactionType.CashWithdraw;
                else if (rbWithdrawFromAccount.IsChecked == true) type = TransactionType.WithdrawFromAccount;
                else type = TransactionType.WithdrawFromCash;

                var (success, message) = await _accountService.ProcessTransactionAsync(
                    type, amount, txtByWhom.Text.Trim(),
                    txnDate, txtRemarks.Text?.Trim());

                MessageBox.Show(message,
                    success ? "Success" : "Error",
                    MessageBoxButton.OK,
                    success ? MessageBoxImage.Information : MessageBoxImage.Error);

                if (success)
                    DialogResult = true;
            }
            catch (Exception ex)
            {
                string detail = ex.InnerException?.InnerException?.Message
                             ?? ex.InnerException?.Message
                             ?? "No detail";
                MessageBox.Show($"Error:\n{ex.Message}\n\nDetail:\n{detail}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
            => DialogResult = false;
    }
}