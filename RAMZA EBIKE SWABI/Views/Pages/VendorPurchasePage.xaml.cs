using Microsoft.EntityFrameworkCore;
using Ramza_EBike_Swabi.Models;
using Ramza_EBike_Swabi.Services;
using Ramza_EBike_Swabi.Views.Windows;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using static System.Windows.Forms.VisualStyles.VisualStyleElement.Rebar;

namespace Ramza_EBike_Swabi.Views.Pages
{
    // ===========================
    // VIEW MODEL
    // ===========================
    public class BillRowViewModel
    {
        public VendorBill Bill { get; set; } = null!;
        public bool IsMatch { get; set; } = false;

        public int BillId => Bill?.Id ?? 0;
        public string BillDate => Bill?.BillDate.ToString("dd-MMM-yyyy") ?? "";
        public string FirstDocNumber => Bill?.Items?.FirstOrDefault()?.DocumentNumber ?? "";
        public string PaySource => Bill?.PaymentSource ?? "";

        public decimal PreviousBalance => Bill?.PreviousBalance ?? 0m;
        public decimal TotalAmount => Bill?.TotalAmount ?? 0m;
        public decimal AmountPaid => Bill?.AmountPaid ?? 0m;

        // NetBill = PreviousBalance + TotalAmount − AmountPaid (bill-level only)
        public decimal NetBill => PreviousBalance + TotalAmount - AmountPaid;
        public decimal RemainingBalance => Bill?.RemainingBalance ?? 0m;
        public decimal TotalCommission => Bill?.TotalCommission ?? 0m;
        public decimal TotalTaxPaid => Bill?.TotalTaxPaid ?? 0m;
    }

    public partial class VendorPurchasePage : Page
    {
        private readonly VendorService _service = new();
        private readonly VendorCashService _vendorCashService = new();
        private readonly AccountService _accountService = new();
        private readonly MainLayout _layout;
        private Vendor? _vendor;
        private List<BillRowViewModel> _allRows = new();

        public VendorPurchasePage(MainLayout layout, int vendorId)
        {
            InitializeComponent();
            _layout = layout;
            LoadVendor(vendorId);
        }

        private async void LoadVendor(int vendorId)
        {
            try
            {
                _vendor = await _service.GetVendorAsync(vendorId);
                if (_vendor == null) { MessageBox.Show("Vendor not found!"); return; }

                VendorName.Text = $"Vendor: {_vendor.VendorName}";
                VendorId.Text = $"Vendor ID: {_vendor.Id}";

                var bills = await _service.GetBillsForVendorAsync(_vendor.Id);
                _vendor.Bills = bills;

                // ── TRUE remaining = lastBill.RemainingBalance − SUM(standalone payments) ──
                decimal remaining = await _service.GetTrueRemainingBalanceAsync(_vendor.Id);
                VendorRemainingBalance.Text = $"PKR {remaining:N2}";

                if (PayRemainingBtn != null)
                    PayRemainingBtn.IsEnabled = remaining > 0;

                decimal vendorCash = await _vendorCashService.GetVendorCashBalanceAsync(_vendor.Id);
                if (VendorCashBalanceText != null)
                    VendorCashBalanceText.Text = $"PKR {vendorCash:N2}";

                // Bill table rows are unchanged — show bill-level figures as entered
                _allRows = bills
                    .OrderByDescending(b => b.Id)
                    .Select(b => new BillRowViewModel { Bill = b })
                    .ToList();

                BillsGrid.ItemsSource = null;
                BillsGrid.ItemsSource = _allRows;

                ApplySearch();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading vendor: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===========================
        // SEARCH
        // ===========================
        private void SearchTypeCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SearchBox == null) return;
            SearchBox.Text = string.Empty;
            ClearHighlights();
            SearchBox.Focus();
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e) => ApplySearch();

        private void ClearSearch_Click(object sender, RoutedEventArgs e)
        {
            SearchBox.Text = string.Empty;
            ClearHighlights();
        }

        private void ApplySearch()
        {
            string keyword = SearchBox?.Text?.Trim() ?? "";
            if (string.IsNullOrWhiteSpace(keyword)) { ClearHighlights(); return; }

            string kw = keyword.ToLower();
            int typeIndex = SearchTypeCombo?.SelectedIndex ?? 0;
            int matchCount = 0;

            foreach (var row in _allRows)
            {
                if (row.Bill?.Items == null || row.Bill.Items.Count == 0)
                { row.IsMatch = false; continue; }

                bool matched = typeIndex switch
                {
                    0 => row.Bill.Items.Any(i => i.DocumentNumber?.ToLower().Contains(kw) == true),
                    1 => row.Bill.Items.Any(i => i.Model?.ToLower().Contains(kw) == true),
                    2 => row.Bill.Items.Any(i => i.ChassisNumber?.ToLower().Contains(kw) == true),
                    3 => row.Bill.Items.Any(i => i.MotorNumber?.ToLower().Contains(kw) == true),
                    _ => false
                };   

                row.IsMatch = matched;
                if (matched) matchCount++;
            }

            BillsGrid.Items.Refresh();
            string label = (SearchTypeCombo?.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "";
            SearchResultHint.Text = matchCount > 0
                ? $"{matchCount} bill(s) highlighted"
                : $"No match found for this {label}";
        }

        private void ClearHighlights()
        {
            foreach (var row in _allRows) row.IsMatch = false;
            if (BillsGrid?.ItemsSource != null) BillsGrid.Items.Refresh();
            if (SearchResultHint != null) SearchResultHint.Text = string.Empty;
        }

        // ===========================
        // PAY REMAINING BALANCE
        // ===========================
        private async void PayRemaining_Click(object sender, RoutedEventArgs e)
        {
            if (_vendor == null) return;

            // Always fetch the true remaining (bills − standalone payments)
            decimal remaining = await _service.GetTrueRemainingBalanceAsync(_vendor.Id);

            if (remaining <= 0)
            {
                MessageBox.Show("This vendor has no outstanding balance to pay.",
                    "No Balance", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var win = new VendorPaymentWindow(_vendor.Id, _vendor.VendorName, remaining)
            {
                Owner = Window.GetWindow(this)
            };

            if (win.ShowDialog() == true)
            {
                // Reload the full page so header balance updates immediately
                LoadVendor(_vendor.Id);
            }
        }

        


    private void VendorLedger_Click(object sender, RoutedEventArgs e)
        {
            if (_vendor == null) return;
            _layout.Navigate(new VendorLedgerPage(_layout, _vendor.Id, _vendor.VendorName));
        }

        // ===========================
        // PAYMENT HISTORY
        // ===========================
        private void PaymentHistory_Click(object sender, RoutedEventArgs e)
        {
            if (_vendor == null) return;
            _layout.Navigate(new VendorPaymentHistoryPage(_layout, _vendor.Id, _vendor.VendorName));
        }

        // ===========================
        // BILL ACTIONS
        // ===========================
        private void AddBill_Click(object sender, RoutedEventArgs e)
        {
            if (_vendor != null)
                _layout.Navigate(new VendorBillForm(_layout, _vendor.Id));
        }

        private void ViewBill_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not BillRowViewModel vm) return;
            _layout.Navigate(new VendorBillDetailPage(_layout, vm.Bill.Id));
        }

        private void EditBill_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not BillRowViewModel vm) return;
            _layout.Navigate(new VendorBillForm(_layout, vm.Bill.VendorId, vm.Bill.Id));
        }

        private async void DeleteBill_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not BillRowViewModel vm) return;

            var confirm = MessageBox.Show(
                $"Are you sure you want to delete Bill #{vm.Bill.Id}?\n\nYou can undo this action.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            await _service.SoftDeleteBillAsync(vm.Bill.Id);

            var undo = MessageBox.Show("Bill deleted.\n\nDo you want to undo?",
                "Undo Delete", MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (undo == MessageBoxResult.Yes)
                await _service.RestoreBillAsync(vm.Bill.Id);

            if (_vendor != null) LoadVendor(_vendor.Id);
        }

        // ===========================
        // CLOSE ALL BILLS
        // ===========================
        private async void CloseBills_Click(object sender, RoutedEventArgs e)
        {
            if (_vendor == null) return;

            var bills = _allRows.Select(r => r.Bill).ToList();
            if (bills.Count == 0)
            {
                MessageBox.Show("No bills to close.", "Info",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            decimal remaining = await _service.GetTrueRemainingBalanceAsync(_vendor.Id);

            var confirm = MessageBox.Show(
                $"Close ALL bills for {_vendor.VendorName}?\n\n" +
                $"• {bills.Count} bill(s) will be permanently deleted\n" +
                $"• Current remaining balance: PKR {remaining:N2}\n" +
                $"• All bill transaction history will be cleared\n\n" +
                $"This action cannot be undone.",
                "Confirm Close All Bills", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                using var db = new Ramza_EBike_Swabi.Data.AppDbContext();

                var allBills = await db.VendorBill
                    .IgnoreQueryFilters()
                    .Where(b => b.VendorId == _vendor.Id)
                    .Include(b => b.Items)
                    .ToListAsync();

                foreach (var bill in allBills)
                {
                    db.VendorBillItem.RemoveRange(bill.Items);
                    db.VendorBill.Remove(bill);
                }

                if (remaining > 0)
                {
                    var balance = await db.AccountBalances.FirstOrDefaultAsync();
                    if (balance != null)
                    {
                        db.AccountTransactions.Add(new AccountTransaction
                        {
                            Type = TransactionType.CashDeposit,
                            ByWhom = _vendor.VendorName,
                            Amount = 0,
                            TransactionDate = DateTime.Now,
                            Remarks = $"Bills CLOSED for vendor {_vendor.VendorName} — Outstanding balance PKR {remaining:N2} written off",
                            CashBalanceAfter = balance.CashBalance,
                            BankBalanceAfter = balance.BankBalance
                        });
                    }
                }

                await db.SaveChangesAsync();

                MessageBox.Show(
                    $"All bills for {_vendor.VendorName} have been closed and deleted.",
                    "Bills Closed", MessageBoxButton.OK, MessageBoxImage.Information);

                LoadVendor(_vendor.Id);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error closing bills: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ===========================
        // PRINT
        // ===========================
        private void PrintBill_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not BillRowViewModel vm) return;
            var detailPage = new VendorBillDetailPage(_layout, vm.Bill.Id);
            _layout.Navigate(detailPage);
            detailPage.Dispatcher.InvokeAsync(
                () => detailPage.ExportToPdf(),
                System.Windows.Threading.DispatcherPriority.Loaded);
        }

        private void Back_Click(object sender, RoutedEventArgs e)
            => _layout.Navigate(new VendorPage(_layout));
    }
}