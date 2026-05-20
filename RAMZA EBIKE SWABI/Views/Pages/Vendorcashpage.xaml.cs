using Microsoft.EntityFrameworkCore;
using Ramza_EBike_Swabi.Data;
using Ramza_EBike_Swabi.Models;
using Ramza_EBike_Swabi.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Ramza_EBike_Swabi.Views.Pages
{
    // ───────────────────────────────────────────────
    // DISPLAY ROW
    // ───────────────────────────────────────────────
    public class VendorCashLedgerRow
    {
        public int Id { get; set; }
        public int VendorId { get; set; }
        public DateTime TransactionDate { get; set; }
        public string VendorName { get; set; } = string.Empty;
        public string TypeDisplay { get; set; } = string.Empty;
        public string ByWhom { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Source { get; set; } = string.Empty;
        public decimal VendorCashBalanceAfter { get; set; }
        public string? Remarks { get; set; }
        public VendorCashType RawType { get; set; }

        public static VendorCashLedgerRow From(VendorCashLedger l) => new()
        {
            Id = l.Id,
            VendorId = l.VendorId,
            TransactionDate = l.TransactionDate,
            VendorName = l.VendorName ?? string.Empty,
            TypeDisplay = l.Type switch
            {
                VendorCashType.Added => "Cash Added",
                VendorCashType.Applied => "Applied to Bill Balance",
                VendorCashType.Refunded => "Refunded",
                _ => l.Type.ToString()
            },
            RawType = l.Type,
            ByWhom = l.ByWhom ?? string.Empty,
            Amount = l.Amount,
            Source = l.Source ?? string.Empty,
            VendorCashBalanceAfter = l.VendorCashBalanceAfter,
            Remarks = l.Remarks
        };
    }

    // ───────────────────────────────────────────────
    // VENDOR SUMMARY (left panel cards)
    // ───────────────────────────────────────────────
    public class VendorCashSummary
    {
        public int VendorId { get; set; }
        public string VendorName { get; set; } = string.Empty;
        public decimal CashBalance { get; set; }
        public decimal RemainingBill { get; set; }
    }

    // ───────────────────────────────────────────────
    // PAGE
    // ───────────────────────────────────────────────
    public partial class VendorCashPage : Page
    {
        private readonly MainLayout _layout;
        private readonly VendorCashService _cashService;
        private readonly VendorService _vendorService;

        private List<VendorCashSummary> _vendorSummaries = new();
        private List<VendorCashLedgerRow> _allRows = new();
        private int? _selectedVendorId = null;

        public VendorCashPage(MainLayout layout)
        {
            InitializeComponent();

            _layout = layout;
            _cashService = new VendorCashService();
            _vendorService = new VendorService();

            try
            {
                dpFrom.SelectedDate = DateTime.Today.AddDays(-30);
                dpTo.SelectedDate = DateTime.Today;
            }
            catch { /* safe */ }

            Loaded += Page_Loaded;
        }

        private async void Page_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await RefreshAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading page:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════
        // REFRESH ALL
        // ══════════════════════════════════════════
        private async Task RefreshAll()
        {
            await LoadVendorSummaries();
            await LoadLedger();
            UpdateSummaryCards();
            UpdateLedgerTitle();
        }

        // ══════════════════════════════════════════
        // LOAD VENDOR SUMMARIES — use VendorService for true remaining
        // ══════════════════════════════════════════
        private async Task LoadVendorSummaries()
        {
            try
            {
                using var db = new AppDbContext();

                // Load all vendors
                var vendors = await db.Vendor
                    .OrderBy(v => v.VendorName)
                    .ToListAsync();

                // Load all vendor cash balances in one query
                var cashBalances = await db.VendorCashBalances.ToListAsync();

                _vendorSummaries = new List<VendorCashSummary>();

                foreach (var v in vendors)
                {
                    decimal cash = cashBalances.FirstOrDefault(c => c.VendorId == v.Id)?.Balance ?? 0m;

                    // ✅ Use VendorService to get TRUE remaining balance
                    decimal remaining = await _vendorService.GetTrueRemainingBalanceAsync(v.Id);

                    _vendorSummaries.Add(new VendorCashSummary
                    {
                        VendorId = v.Id,
                        VendorName = v.VendorName ?? $"Vendor #{v.Id}",
                        CashBalance = cash,
                        RemainingBill = remaining
                    });
                }

                BuildVendorPanel();
                RebuildVendorFilterCombo();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading vendors:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════
        // LOAD LEDGER — direct DB
        // ══════════════════════════════════════════
        private async Task LoadLedger()
        {
            try
            {
                using var db = new AppDbContext();

                IQueryable<VendorCashLedger> query = db.VendorCashLedger;

                if (_selectedVendorId.HasValue)
                    query = query.Where(l => l.VendorId == _selectedVendorId.Value);

                var ledger = await query
                    .OrderByDescending(l => l.TransactionDate)
                    .ToListAsync();

                _allRows = ledger.Select(VendorCashLedgerRow.From).ToList();
                ApplyFilter();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error loading ledger:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ──────────────────────────────────────────
        // LEFT PANEL — Vendor Cards with TRUE remaining
        // ──────────────────────────────────────────
        private void BuildVendorPanel()
        {
            if (VendorListPanel == null) return;
            VendorListPanel.Children.Clear();

            // "All Vendors" card
            VendorListPanel.Children.Add(MakeVendorCard(
                null, "All Vendors",
                _vendorSummaries.Sum(v => v.CashBalance),
                _vendorSummaries.Sum(v => v.RemainingBill)));

            foreach (var vs in _vendorSummaries)
                VendorListPanel.Children.Add(
                    MakeVendorCard(vs.VendorId, vs.VendorName, vs.CashBalance, vs.RemainingBill));
        }

        private Border MakeVendorCard(int? vendorId, string name, decimal cash, decimal remaining)
        {
            bool isSelected = (vendorId == _selectedVendorId);

            var border = new Border
            {
                Background = isSelected
                                    ? new SolidColorBrush(Color.FromRgb(219, 234, 254))
                                    : Brushes.White,
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(12, 10, 12, 10),
                Margin = new Thickness(0, 0, 0, 6),
                BorderBrush = isSelected
                                    ? new SolidColorBrush(Color.FromRgb(59, 130, 246))
                                    : new SolidColorBrush(Color.FromRgb(226, 232, 240)),
                BorderThickness = new Thickness(isSelected ? 2 : 1),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            var sp = new StackPanel();

            // Vendor Name
            sp.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 13,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(27, 42, 74)),
                TextWrapping = TextWrapping.Wrap
            });

            // Cash Balance Row
            var cashRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 5, 0, 0)
            };
            cashRow.Children.Add(new TextBlock
            {
                Text = "Cash: ",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102))
            });
            cashRow.Children.Add(new TextBlock
            {
                Text = $"PKR {cash:N0}",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(27, 138, 61))
            });
            sp.Children.Add(cashRow);

            // Remaining Bill Row
            var billRow = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Margin = new Thickness(0, 2, 0, 0)
            };
            billRow.Children.Add(new TextBlock
            {
                Text = "Bill: ",
                FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(102, 102, 102))
            });
            billRow.Children.Add(new TextBlock
            {
                Text = $"PKR {remaining:N0}",
                FontSize = 11,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush(Color.FromRgb(192, 57, 43))
            });
            sp.Children.Add(billRow);

            border.Child = sp;

            border.MouseLeftButtonUp += async (s, e2) =>
            {
                try
                {
                    _selectedVendorId = vendorId;
                    await RefreshAll();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error: {ex.Message}");
                }
            };

            return border;
        }

        private void UpdateLedgerTitle()
        {
            if (txtLedgerTitle == null) return;

            if (_selectedVendorId == null)
            {
                txtLedgerTitle.Text = "All Transaction History";
                if (txtLedgerVendorBalance != null)
                    txtLedgerVendorBalance.Text = string.Empty;
            }
            else
            {
                var vs = _vendorSummaries.FirstOrDefault(v => v.VendorId == _selectedVendorId);
                if (vs != null)
                {
                    txtLedgerTitle.Text = $"{vs.VendorName} — Transactions";
                    if (txtLedgerVendorBalance != null)
                        txtLedgerVendorBalance.Text =
                            $"Cash: PKR {vs.CashBalance:N2}  |  Bill: PKR {vs.RemainingBill:N2}";
                }
            }
        }

        private void RebuildVendorFilterCombo()
        {
            if (cmbFilterVendor == null) return;

            while (cmbFilterVendor.Items.Count > 1)
                cmbFilterVendor.Items.RemoveAt(1);

            foreach (var vs in _vendorSummaries)
            {
                cmbFilterVendor.Items.Add(
                    new ComboBoxItem { Content = vs.VendorName, Tag = vs.VendorId });
            }
        }

        private void UpdateSummaryCards()
        {
            if (txtTotalVendorCash != null)
                txtTotalVendorCash.Text = $"PKR {_vendorSummaries.Sum(v => v.CashBalance):N2}";

            if (txtTotalRemaining != null)
                txtTotalRemaining.Text = $"PKR {_vendorSummaries.Sum(v => v.RemainingBill):N2}";

            if (txtVendorsWithCash != null)
                txtVendorsWithCash.Text = _vendorSummaries.Count(v => v.CashBalance > 0).ToString();

            if (txtAddedToday != null)
            {
                decimal today = _allRows
                    .Where(r => r.TransactionDate.Date == DateTime.Today
                             && r.RawType == VendorCashType.Added)
                    .Sum(r => r.Amount);
                txtAddedToday.Text = $"PKR {today:N2}";
            }
        }

        // ══════════════════════════════════════════
        // FILTER
        // ══════════════════════════════════════════
        private void ApplyFilter()
        {
            if (LedgerGrid == null) return;

            var rows = _allRows.AsEnumerable();

            int typeIdx = cmbFilterType?.SelectedIndex ?? 0;
            if (typeIdx == 1) rows = rows.Where(r => r.RawType == VendorCashType.Added);
            if (typeIdx == 2) rows = rows.Where(r => r.RawType == VendorCashType.Applied);

            var result = rows.ToList();
            LedgerGrid.ItemsSource = result;

            if (txtRecordCount != null)
                txtRecordCount.Text = $"{result.Count} record(s)";
        }

        private void Filter_Click(object sender, RoutedEventArgs e)
        {
            if (dpFrom.SelectedDate == null || dpTo.SelectedDate == null)
            { MessageBox.Show("Please select both From and To dates."); return; }

            var from = dpFrom.SelectedDate.Value.Date;
            var to = dpTo.SelectedDate.Value.Date;
            if (from > to) { MessageBox.Show("From date cannot be later than To date."); return; }

            var rows = _allRows
                .Where(r => r.TransactionDate.Date >= from && r.TransactionDate.Date <= to)
                .ToList();

            int typeIdx = cmbFilterType?.SelectedIndex ?? 0;
            if (typeIdx == 1) rows = rows.Where(r => r.RawType == VendorCashType.Added).ToList();
            if (typeIdx == 2) rows = rows.Where(r => r.RawType == VendorCashType.Applied).ToList();

            LedgerGrid.ItemsSource = rows;
            if (txtRecordCount != null)
                txtRecordCount.Text = $"{rows.Count} record(s)";
        }

        private async void Reset_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                dpFrom.SelectedDate = DateTime.Today.AddDays(-30);
                dpTo.SelectedDate = DateTime.Today;
                if (cmbFilterType != null) cmbFilterType.SelectedIndex = 0;
                if (cmbFilterVendor != null) cmbFilterVendor.SelectedIndex = 0;
                _selectedVendorId = null;
                await RefreshAll();
            }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
        }

        private async void FilterVendor_Changed(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                if (cmbFilterVendor?.SelectedItem is ComboBoxItem item && item.Tag is int vid)
                    _selectedVendorId = vid;
                else
                    _selectedVendorId = null;

                await LoadLedger();
                UpdateLedgerTitle();
                UpdateSummaryCards();
            }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
        }

        private void FilterType_Changed(object sender, SelectionChangedEventArgs e)
            => ApplyFilter();

        // ══════════════════════════════════════════
        // ADD CASH BUTTON
        // ══════════════════════════════════════════
        private async void AddCash_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var win = new AddVendorCashWindow(_selectedVendorId)
                {
                    Owner = Window.GetWindow(this)
                };

                if (win.ShowDialog() == true)
                    await RefreshAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error opening Add Cash window:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════
        // EDIT — with Account Transaction update
        // ══════════════════════════════════════════
        private async void EditLedger_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not VendorCashLedgerRow row) return;

            try
            {
                var win = new EditVendorCashLedgerWindow(row)
                {
                    Owner = Window.GetWindow(this)
                };
                if (win.ShowDialog() != true) return;

                using var db = new AppDbContext();

                var entry = await db.VendorCashLedger.FindAsync(row.Id);
                if (entry == null)
                {
                    MessageBox.Show("Record not found.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Store old values for account transaction update
                decimal oldAmount = entry.Amount;
                string oldSource = entry.Source ?? "Cash";

                // Update ledger entry
                entry.ByWhom = win.EditedByWhom;
                entry.Remarks = win.EditedRemarks;
                entry.TransactionDate = win.EditedDate;
                entry.Amount = win.EditedAmount;
                entry.Source = win.EditedSource;

                // ✅ Update Account Transaction ONLY if this is "Cash Added" type
                if (entry.Type == VendorCashType.Added)
                {
                    // Find related account transaction
                    var accountTxn = await db.AccountTransactions
                        .Where(t => t.TransactionDate.Date == row.TransactionDate.Date
                                 && t.Remarks != null
                                 && t.Remarks.Contains(row.VendorName))
                        .OrderByDescending(t => t.Id)
                        .FirstOrDefaultAsync();

                    if (accountTxn != null)
                    {
                        // Update account transaction amount and source
                        accountTxn.Amount = win.EditedAmount;
                        accountTxn.ByWhom = win.EditedByWhom;
                        accountTxn.TransactionDate = win.EditedDate;

                        // Update transaction type based on source
                        accountTxn.Type = win.EditedSource == "Cash"
                            ? TransactionType.WithdrawFromCash
                            : TransactionType.CashWithdraw;
                    }

                    // Update vendor cash balance if amount changed
                    if (oldAmount != win.EditedAmount)
                    {
                        var vcb = await db.VendorCashBalances
                            .FirstOrDefaultAsync(v => v.VendorId == entry.VendorId);

                        if (vcb != null)
                        {
                            decimal difference = win.EditedAmount - oldAmount;
                            vcb.Balance += difference;
                        }
                    }

                    // Update account balance if source or amount changed
                    var accountBalance = await db.AccountBalances.FirstOrDefaultAsync();
                    if (accountBalance != null)
                    {
                        // Reverse old transaction
                        if (oldSource == "Cash")
                            accountBalance.CashBalance += oldAmount;
                        else
                            accountBalance.BankBalance += oldAmount;

                        // Apply new transaction
                        if (win.EditedSource == "Cash")
                            accountBalance.CashBalance -= win.EditedAmount;
                        else
                            accountBalance.BankBalance -= win.EditedAmount;

                        // Update account transaction balances
                        if (accountTxn != null)
                        {
                            accountTxn.CashBalanceAfter = accountBalance.CashBalance;
                            accountTxn.BankBalanceAfter = accountBalance.BankBalance;
                        }
                    }
                }

                // Recalculate running balances
                await RecalcLedgerBalancesAsync(db, entry.VendorId);
                await db.SaveChangesAsync();
                await RefreshAll();

                MessageBox.Show("Transaction updated successfully.", "Saved",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error saving edit:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════
        // DELETE
        // ══════════════════════════════════════════
        private async void DeleteLedger_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.DataContext is not VendorCashLedgerRow row) return;

            var confirm = MessageBox.Show(
                $"Delete this transaction?\n\n" +
                $"Vendor : {row.VendorName}\n" +
                $"Amount : PKR {row.Amount:N2}\n" +
                $"Date   : {row.TransactionDate:dd-MMM-yyyy}\n\n" +
                $"Subsequent balances will be recalculated.",
                "Confirm Delete", MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                int vendorId;
                decimal removedAmount;
                VendorCashType removedType;
                string removedSource;

                using (var db = new AppDbContext())
                {
                    var entry = await db.VendorCashLedger.FindAsync(row.Id);
                    if (entry == null) return;

                    vendorId = entry.VendorId;
                    removedAmount = entry.Amount;
                    removedType = entry.Type;
                    removedSource = entry.Source ?? "Cash";

                    // Reverse vendor cash balance for "Added" entries
                    if (removedType == VendorCashType.Added)
                    {
                        var vcb = await db.VendorCashBalances
                            .FirstOrDefaultAsync(v => v.VendorId == vendorId);
                        if (vcb != null)
                            vcb.Balance = Math.Max(0, vcb.Balance - removedAmount);

                        // Reverse account balance
                        var accountBalance = await db.AccountBalances.FirstOrDefaultAsync();
                        if (accountBalance != null)
                        {
                            if (removedSource == "Cash")
                                accountBalance.CashBalance += removedAmount;
                            else
                                accountBalance.BankBalance += removedAmount;
                        }

                        // Delete related account transaction
                        var accountTxn = await db.AccountTransactions
                            .Where(t => t.TransactionDate.Date == row.TransactionDate.Date
                                     && t.Remarks != null
                                     && t.Remarks.Contains(row.VendorName))
                            .OrderByDescending(t => t.Id)
                            .FirstOrDefaultAsync();

                        if (accountTxn != null)
                            db.AccountTransactions.Remove(accountTxn);
                    }

                    db.VendorCashLedger.Remove(entry);
                    await db.SaveChangesAsync();
                }

                // Recalculate in a fresh context
                using (var db2 = new AppDbContext())
                {
                    await RecalcLedgerBalancesAsync(db2, vendorId);
                    await db2.SaveChangesAsync();
                }

                await RefreshAll();
                MessageBox.Show("Deleted and balances updated.", "Deleted",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════
        // CLEAR HISTORY
        // ══════════════════════════════════════════
        private async void ClearHistory_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "Clear ALL vendor cash transaction history?\n\n" +
                "• All transaction records will be deleted\n" +
                "• Vendor cash balances will remain unchanged\n" +
                "• This action cannot be undone\n\n" +
                "Continue?",
                "Confirm Clear History",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                using var db = new AppDbContext();

                // Delete all ledger entries
                var allEntries = await db.VendorCashLedger.ToListAsync();
                db.VendorCashLedger.RemoveRange(allEntries);

                await db.SaveChangesAsync();

                MessageBox.Show(
                    $"Transaction history cleared successfully.\n{allEntries.Count} record(s) removed.",
                    "History Cleared",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                await RefreshAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error clearing history:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════
        // RECALCULATE RUNNING BALANCES
        // ══════════════════════════════════════════
        private static async Task RecalcLedgerBalancesAsync(AppDbContext db, int vendorId)
        {
            var all = await db.VendorCashLedger
                .Where(l => l.VendorId == vendorId)
                .OrderBy(l => l.TransactionDate)
                .ThenBy(l => l.Id)
                .ToListAsync();

            var vcb = await db.VendorCashBalances
                .FirstOrDefaultAsync(v => v.VendorId == vendorId);

            decimal running = 0;
            foreach (var item in all)
            {
                switch (item.Type)
                {
                    case VendorCashType.Added:
                        running += item.Amount; break;
                    case VendorCashType.Applied:
                    case VendorCashType.Refunded:
                        running -= item.Amount; break;
                }
                if (running < 0) running = 0;
                item.VendorCashBalanceAfter = running;
            }

            if (vcb != null) vcb.Balance = running;
        }

        // ══════════════════════════════════════════
        // BACK
        // ══════════════════════════════════════════
        private void Back_Click(object sender, RoutedEventArgs e)
        {
            try { _layout.Navigate(new DashboardPage(_layout)); }
            catch (Exception ex) { MessageBox.Show($"Error: {ex.Message}"); }
        }
    }
}