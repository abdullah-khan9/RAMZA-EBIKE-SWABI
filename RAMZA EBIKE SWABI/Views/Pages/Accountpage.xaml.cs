using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Ramza_EBike_Swabi.Models;
using Ramza_EBike_Swabi.Services;

namespace Ramza_EBike_Swabi.Views.Pages
{
    public class TransactionDisplayRow
    {
        public int Id { get; set; }
        public DateTime TransactionDate { get; set; }
        public string TypeDisplay { get; set; } = string.Empty;
        public string ByWhom { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public decimal CashBalanceAfter { get; set; }
        public decimal BankBalanceAfter { get; set; }
        public string? Remarks { get; set; }

        // ✅ Green = money IN, Red = money OUT
        public bool IsIncoming { get; set; }
        public SolidColorBrush RowColor => IsIncoming
            ? new SolidColorBrush(Color.FromRgb(0xE8, 0xF5, 0xE9))   // light green
            : new SolidColorBrush(Color.FromRgb(0xFF, 0xEB, 0xEE));   // light red
        public SolidColorBrush AmountColor => IsIncoming
            ? new SolidColorBrush(Color.FromRgb(0x1B, 0x5E, 0x20))   // dark green
            : new SolidColorBrush(Color.FromRgb(0xB7, 0x1C, 0x1C));   // dark red

        public static TransactionDisplayRow From(AccountTransaction t)
        {
            // ✅ Incoming = money coming IN to cash or account
            bool isIncoming = t.Type is
                TransactionType.CashDeposit or
                TransactionType.DepositToAccount or
                TransactionType.CashWithdraw;         // Account→Cash: cash mein aata hai

            // ConvertCashToAccount: cash out → account in (neutral, treat as outgoing from cash)
            if (t.Type == TransactionType.ConvertCashToAccount)
                isIncoming = false;

            return new TransactionDisplayRow
            {
                Id = t.Id,
                TransactionDate = t.TransactionDate,
                TypeDisplay = t.Type switch
                {
                    TransactionType.CashDeposit => "Cash Deposited",
                    TransactionType.DepositToAccount => "Deposited to Account",
                    TransactionType.CashWithdraw => "Withdrawn from Account to Cash",
                    TransactionType.ConvertCashToAccount => "Cash Converted to Account",
                    TransactionType.WithdrawFromCash => "Withdrawn / Spent from Cash",
                    TransactionType.WithdrawFromAccount => "Withdrawn / Spent from Account",
                    _ => t.Type.ToString()
                },
                ByWhom = t.ByWhom,
                Amount = t.Amount,
                CashBalanceAfter = t.CashBalanceAfter,
                BankBalanceAfter = t.BankBalanceAfter,
                Remarks = t.Remarks,
                IsIncoming = isIncoming
            };
        }
    }

    public class ProfitDisplayRow
    {
        public int Id { get; set; }
        public string InvoiceRef { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public DateTime SaleDate { get; set; }
        public int BikeCount { get; set; }
        public string BikeModels { get; set; } = string.Empty;
        public decimal TotalSalePrice { get; set; }
        public decimal TotalWholesaleCost { get; set; }
        public decimal Discount { get; set; }
        public decimal Profit { get; set; }
        public string? Remarks { get; set; }

        public static ProfitDisplayRow From(ProfitRecord p) => new()
        {
            Id = p.Id,
            InvoiceRef = $"INV-{p.CustomerInvoiceId:D4}",
            CustomerName = p.CustomerName,
            SaleDate = p.SaleDate,
            BikeCount = p.BikeCount,
            BikeModels = p.BikeModels,
            TotalSalePrice = p.TotalSalePrice,
            TotalWholesaleCost = p.TotalWholesaleCost,
            Discount = p.Discount,
            Profit = p.Profit,
            Remarks = $"INV-{p.CustomerInvoiceId:D4} | {p.CustomerName}" +
                      (string.IsNullOrWhiteSpace(p.Remarks) ? "" : $" — {p.Remarks}")
        };
    }

    public class VendorCashDisplayRow
    {
        public int Id { get; set; }
        public DateTime TransactionDate { get; set; }
        public string VendorName { get; set; } = string.Empty;
        public string TypeDisplay { get; set; } = string.Empty;
        public string ByWhom { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Source { get; set; } = string.Empty;
        public decimal VendorCashBalanceAfter { get; set; }
        public string? Remarks { get; set; }

        public static VendorCashDisplayRow From(VendorCashLedger l) => new()
        {
            Id = l.Id,
            TransactionDate = l.TransactionDate,
            VendorName = l.VendorName,
            TypeDisplay = l.Type switch
            {
                VendorCashType.Added => "Cash Added",
                VendorCashType.Applied => "Applied to Bill Balance",
                VendorCashType.Refunded => "Refunded",
                _ => l.Type.ToString()
            },
            ByWhom = l.ByWhom,
            Amount = l.Amount,
            Source = l.Source,
            VendorCashBalanceAfter = l.VendorCashBalanceAfter,
            Remarks = l.Remarks
        };
    }

    public partial class AccountPage : Page
    {
        private readonly AccountService _accountService = new();
        private readonly ProfitService _profitService = new();
        private readonly VendorCashService _vendorCashService = new();
        private readonly MainLayout _layout;

        public AccountPage(MainLayout layout)
        {
            InitializeComponent();
            _layout = layout;
            dpFrom.SelectedDate = DateTime.Today.AddDays(-30);
            dpTo.SelectedDate = DateTime.Today;
            LoadAll();
        }

        // ✅ Helper — Cash tab mein kya aaye
        private static bool IsCashTransaction(TransactionDisplayRow t) =>
            t.TypeDisplay == "Cash Deposited" ||
            t.TypeDisplay == "Withdrawn from Account to Cash" ||
            t.TypeDisplay == "Cash Converted to Account" ||
            t.TypeDisplay == "Withdrawn / Spent from Cash";

        // ✅ Helper — Account tab mein kya aaye
        private static bool IsAccountTransaction(TransactionDisplayRow t) =>
            t.TypeDisplay == "Deposited to Account" ||
            t.TypeDisplay == "Withdrawn / Spent from Account" ||
            t.TypeDisplay == "Withdrawn from Account to Cash" ||
            t.TypeDisplay == "Cash Converted to Account";

        private async void LoadAll()
        {
            try
            {
                var balance = await _accountService.GetBalanceAsync();
                decimal vendorCash = await _vendorCashService.GetTotalVendorCashAsync();

                txtCashBalance.Text = $"PKR {balance.CashBalance:N2}";
                txtAccountBalance.Text = $"PKR {balance.BankBalance:N2}";
                txtVendorCash.Text = $"PKR {vendorCash:N2}";
                txtTotalBalance.Text = $"PKR {balance.CashBalance + balance.BankBalance + vendorCash:N2}";

                decimal totalProfit = await _profitService.GetTotalProfitAsync();
                txtTotalProfit.Text = $"PKR {totalProfit:N2}";

                var txns = await _accountService.GetRecentTransactionsAsync(10);
                var allRows = txns.Select(TransactionDisplayRow.From).ToList();
                dgTransactions.ItemsSource = allRows;

                // ✅ Cash tab
                dgCashTransactions.ItemsSource = allRows.Where(IsCashTransaction).ToList();

                // ✅ Account tab
                dgAccountTransactions.ItemsSource = allRows.Where(IsAccountTransaction).ToList();

                var cashList = allRows.Where(IsCashTransaction).ToList();
                var accountList = allRows.Where(IsAccountTransaction).ToList();
                txnCountHint.Text = $"Showing recent 10 transactions (Cash: {cashList.Count}, Account: {accountList.Count})";

                var profits = await _profitService.GetRecentProfitRecordsAsync(10);
                dgProfit.ItemsSource = profits.Select(ProfitDisplayRow.From).ToList();
                txtFilteredProfit.Text = $"PKR {profits.Sum(p => p.Profit):N2}";

                var ledger = await _vendorCashService.GetRecentLedgerAsync(20);
                dgVendorCash.ItemsSource = ledger.Select(VendorCashDisplayRow.From).ToList();
                txtVendorCashTotal.Text = $"PKR {vendorCash:N2}";

                var perVendor = await _vendorCashService.GetAllVendorCashBalancesAsync();
                txtVendorBalanceSummary.Text = perVendor.Count > 0
                    ? string.Join("  |  ", perVendor.Select(v => $"{v.VendorName}: PKR {v.Balance:N2}"))
                    : "No vendor cash balances";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading account data: {ex.Message}");
            }
        }

        private async void Filter_Click(object sender, RoutedEventArgs e)
        {
            if (dpFrom.SelectedDate == null || dpTo.SelectedDate == null)
            { MessageBox.Show("Please select both From and To dates."); return; }

            var from = dpFrom.SelectedDate.Value;
            var to = dpTo.SelectedDate.Value;
            if (from > to) { MessageBox.Show("From date cannot be later than To date."); return; }

            var txns = await _accountService.GetTransactionsByDateRangeAsync(from, to);
            var allRows = txns.Select(TransactionDisplayRow.From).ToList();
            dgTransactions.ItemsSource = allRows;

            var cashList = allRows.Where(IsCashTransaction).ToList();
            var accountList = allRows.Where(IsAccountTransaction).ToList();

            dgCashTransactions.ItemsSource = cashList;
            dgAccountTransactions.ItemsSource = accountList;

            txnCountHint.Text = $"{allRows.Count} transaction(s) found (Cash: {cashList.Count}, Account: {accountList.Count})";

            var profits = await _profitService.GetProfitByDateRangeAsync(from, to);
            dgProfit.ItemsSource = profits.Select(ProfitDisplayRow.From).ToList();
            txtFilteredProfit.Text = $"PKR {profits.Sum(p => p.Profit):N2}";

            var ledger = await _vendorCashService.GetLedgerByDateRangeAsync(from, to);
            dgVendorCash.ItemsSource = ledger.Select(VendorCashDisplayRow.From).ToList();
        }

        private async void Reset_Click(object sender, RoutedEventArgs e)
        {
            dpFrom.SelectedDate = DateTime.Today.AddDays(-30);
            dpTo.SelectedDate = DateTime.Today;
            LoadAll();
        }

        private async void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Clear ALL transaction history? Balances will NOT be affected.",
                "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;
            await _accountService.ClearTransactionHistoryAsync();
            dgTransactions.ItemsSource = null;
            dgCashTransactions.ItemsSource = null;
            dgAccountTransactions.ItemsSource = null;
            txnCountHint.Text = "Transaction history cleared.";
        }

        private async void ClearProfitHistory_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Clear ALL profit history? Total profit card will reset to 0.",
                "Confirm", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;
            await _profitService.ClearProfitHistoryAsync();
            dgProfit.ItemsSource = null;
            txtFilteredProfit.Text = "PKR 0.00";
            txtTotalProfit.Text = "PKR 0.00";
        }

        private void AddConvert_Click(object sender, RoutedEventArgs e)
        {
            var win = new AccountTransactionWindow { Owner = Window.GetWindow(this) };
            if (win.ShowDialog() == true) LoadAll();
        }

        private void Back_Click(object sender, RoutedEventArgs e)
            => _layout.Navigate(new DashboardPage(_layout));
    }
}