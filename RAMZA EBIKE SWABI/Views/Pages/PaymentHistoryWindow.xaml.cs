// PaymentHistoryWindow.xaml.cs
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using Ramza_EBike_Swabi.Data;
using Ramza_EBike_Swabi.Models;

namespace Ramza_EBike_Swabi.Views.Pages
{
    public partial class PaymentHistoryWindow : Window
    {
        private readonly CustomerInvoice _invoice;

        // ✅ Issue 3: Callback so CustomerDuesPage can reload after changes
        public bool WasModified { get; private set; } = false;

        public PaymentHistoryWindow(CustomerInvoice invoice)
        {
            InitializeComponent();
            _invoice = invoice;
            _ = LoadAsync();
        }

        // ── Load ──────────────────────────────────────────────────────────────
        private async System.Threading.Tasks.Task LoadAsync()
        {
            // Always fetch fresh invoice from DB
            using var db = new AppDbContext();
            var freshInv = await db.CustomerInvoices
                .Include(i => i.Customer)
                .FirstOrDefaultAsync(i => i.CustomerInvoiceId == _invoice.CustomerInvoiceId);

            if (freshInv == null) return;

            string name = freshInv.Customer?.Name ?? "Customer";
            lblTitle.Text = $"Payment History — {name}";
            lblSubtitle.Text =
                $"Invoice #INV-{freshInv.CustomerInvoiceId:D4}   " +
                $"Net Bill: ₨ {freshInv.NetBill:N0}   " +
                $"Total Paid: ₨ {freshInv.AmountPaid:N0}   " +
                $"Remaining: ₨ {freshInv.RemainingBalance:N0}";

            var rows = await db.CustomerPaymentHistories
                .Where(h => h.CustomerInvoiceId == _invoice.CustomerInvoiceId)
                .OrderBy(h => h.PaymentDate)
                .ThenBy(h => h.Id)
                .ToListAsync();

            dgHistory.ItemsSource = rows
                .Select((r, i) => new PaymentHistoryRow
                {
                    RowNumber = i + 1,
                    Id = r.Id,
                    PaymentDate = r.PaymentDate,
                    AmountPaid = r.AmountPaid,
                    AmountPaidCash = r.AmountPaidCash,
                    AmountPaidAccount = r.AmountPaidAccount,
                    RemainingAfter = r.RemainingAfter,
                    PaymentMethod = r.PaymentMethod,
                    ReceivedBy = r.ReceivedBy,
                    CashTransactionId = r.CashTransactionId,
                    AccountTransactionId = r.AccountTransactionId
                }).ToList();
        }

        // ══════════════════════════════════════════════════════════════════════
        // EDIT
        // ══════════════════════════════════════════════════════════════════════
        private async void EditPayment_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not PaymentHistoryRow row) return;

            var win = new EditPaymentWindow(row) { Owner = this };
            if (win.ShowDialog() != true) return;

            try
            {
                using var db = new AppDbContext();

                var history = await db.CustomerPaymentHistories.FindAsync(row.Id);
                if (history == null) return;

                var inv = await db.CustomerInvoices
                    .Include(i => i.Customer)
                    .FirstOrDefaultAsync(i => i.CustomerInvoiceId == history.CustomerInvoiceId);
                if (inv == null) return;

                var balance = await db.AccountBalances.FirstOrDefaultAsync();
                if (balance == null) return;

                decimal oldCash = history.AmountPaidCash;
                decimal oldAccount = history.AmountPaidAccount;
                decimal oldTotal = history.AmountPaid;

                decimal newCash = win.NewCashAmount;
                decimal newAccount = win.NewAccountAmount;
                decimal newTotal = newCash + newAccount;
                decimal diff = newTotal - oldTotal;

                // ── Update invoice totals ──
                inv.AmountPaid = Math.Max(0, inv.AmountPaid + diff);
                inv.AmountPaidCash = Math.Max(0, inv.AmountPaidCash + (newCash - oldCash));
                inv.AmountPaidAccount = Math.Max(0, inv.AmountPaidAccount + (newAccount - oldAccount));
                inv.RemainingBalance = Math.Max(0, inv.RemainingBalance - diff);
                inv.Status = inv.RemainingBalance <= 0 ? "Clear"
                           : inv.AmountPaid == 0 ? "Unpaid"
                                                       : "Partially Paid";

                string invoiceRef = $"INV-{inv.CustomerInvoiceId:D4}";
                string customerName = inv.Customer?.Name ?? history.ReceivedBy;

                // ── ✅ Issue 2: Cash transaction — proper reverse/create/update ──
                decimal cashDiff = newCash - oldCash;

                if (oldCash > 0 && newCash == 0)
                {
                    // ✅ Cash completely removed — delete original transaction + add reversal
                    if (history.CashTransactionId.HasValue)
                    {
                        var oldTxn = await db.AccountTransactions
                            .FindAsync(history.CashTransactionId.Value);
                        if (oldTxn != null) db.AccountTransactions.Remove(oldTxn);
                    }
                    balance.CashBalance -= oldCash;
                    if (balance.CashBalance < 0) balance.CashBalance = 0;
                    history.CashTransactionId = null;
                }
                else if (cashDiff != 0)
                {
                    if (history.CashTransactionId.HasValue)
                    {
                        // Update existing cash transaction
                        var txn = await db.AccountTransactions
                            .FindAsync(history.CashTransactionId.Value);
                        if (txn != null)
                        {
                            txn.Amount = newCash;
                            balance.CashBalance += cashDiff;
                            if (balance.CashBalance < 0) balance.CashBalance = 0;
                            txn.CashBalanceAfter = balance.CashBalance;
                            txn.BankBalanceAfter = balance.BankBalance;
                            txn.Remarks = $"{invoiceRef} | {customerName} | Due payment (Cash) [EDITED: was {oldCash:N0} → now {newCash:N0}]";
                        }
                    }
                    else if (newCash > 0)
                    {
                        // New cash transaction created
                        balance.CashBalance += newCash;
                        var newTxn = new AccountTransaction
                        {
                            Type = TransactionType.CashDeposit,
                            ByWhom = win.NewReceivedBy,
                            Amount = newCash,
                            TransactionDate = win.NewDate,
                            Remarks = $"{invoiceRef} | {customerName} | Due payment (Cash) [ADDED IN EDIT]",
                            CashBalanceAfter = balance.CashBalance,
                            BankBalanceAfter = balance.BankBalance
                        };
                        db.AccountTransactions.Add(newTxn);
                        await db.SaveChangesAsync();
                        history.CashTransactionId = newTxn.Id;
                    }
                }

                // ── ✅ Issue 2: Account transaction — proper reverse/create/update ──
                decimal accountDiff = newAccount - oldAccount;

                if (oldAccount > 0 && newAccount == 0)
                {
                    // Account completely removed — delete original
                    if (history.AccountTransactionId.HasValue)
                    {
                        var oldTxn = await db.AccountTransactions
                            .FindAsync(history.AccountTransactionId.Value);
                        if (oldTxn != null) db.AccountTransactions.Remove(oldTxn);
                    }
                    balance.BankBalance -= oldAccount;
                    if (balance.BankBalance < 0) balance.BankBalance = 0;
                    history.AccountTransactionId = null;
                }
                else if (accountDiff != 0)
                {
                    if (history.AccountTransactionId.HasValue)
                    {
                        // Update existing account transaction
                        var txn = await db.AccountTransactions
                            .FindAsync(history.AccountTransactionId.Value);
                        if (txn != null)
                        {
                            txn.Amount = newAccount;
                            balance.BankBalance += accountDiff;
                            if (balance.BankBalance < 0) balance.BankBalance = 0;
                            txn.CashBalanceAfter = balance.CashBalance;
                            txn.BankBalanceAfter = balance.BankBalance;
                            txn.Remarks = $"{invoiceRef} | {customerName} | Due payment (Account) [EDITED: was {oldAccount:N0} → now {newAccount:N0}]";
                        }
                    }
                    else if (newAccount > 0)
                    {
                        // New account transaction created
                        balance.BankBalance += newAccount;
                        var newTxn = new AccountTransaction
                        {
                            Type = TransactionType.DepositToAccount,
                            ByWhom = win.NewReceivedBy,
                            Amount = newAccount,
                            TransactionDate = win.NewDate,
                            Remarks = $"{invoiceRef} | {customerName} | Due payment (Account) [ADDED IN EDIT]",
                            CashBalanceAfter = balance.CashBalance,
                            BankBalanceAfter = balance.BankBalance
                        };
                        db.AccountTransactions.Add(newTxn);
                        await db.SaveChangesAsync();
                        history.AccountTransactionId = newTxn.Id;
                    }
                }

                // ── Update history record ──
                history.AmountPaid = newTotal;
                history.AmountPaidCash = newCash;
                history.AmountPaidAccount = newAccount;
                history.PaymentDate = win.NewDate;
                history.ReceivedBy = win.NewReceivedBy;
                history.PaymentMethod = (newCash > 0 && newAccount > 0) ? "Cash + Account"
                                          : newAccount > 0 ? "Bank Transfer" : "Cash";

                await db.SaveChangesAsync();

                // ✅ Issue 1: Recalculate RemainingAfter properly
                await RecalculateRemainingAfterAsync(db, inv.CustomerInvoiceId);
                await db.SaveChangesAsync();

                WasModified = true; // ✅ Issue 3: flag for CustomerDuesPage

                MessageBox.Show("Payment updated successfully!", "Updated",
                    MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error updating payment:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // DELETE
        // ══════════════════════════════════════════════════════════════════════
        private async void DeletePayment_Click(object sender, RoutedEventArgs e)
        {
            if ((sender as Button)?.DataContext is not PaymentHistoryRow row) return;

            var confirm = MessageBox.Show(
                $"Delete this payment of ₨ {row.AmountPaid:N0}?\n\n" +
                $"Cash: ₨ {row.AmountPaidCash:N0}   Account: ₨ {row.AmountPaidAccount:N0}\n\n" +
                "Account transactions will be reversed and invoice balance will be updated.",
                "Confirm Delete",
                MessageBoxButton.YesNo, MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                using var db = new AppDbContext();

                var history = await db.CustomerPaymentHistories.FindAsync(row.Id);
                if (history == null) return;

                var inv = await db.CustomerInvoices
                    .FirstOrDefaultAsync(i => i.CustomerInvoiceId == history.CustomerInvoiceId);
                if (inv == null) return;

                var balance = await db.AccountBalances.FirstOrDefaultAsync();
                if (balance == null) return;

                // ── Reverse Cash transaction ──
                if (history.CashTransactionId.HasValue && history.AmountPaidCash > 0)
                {
                    var txn = await db.AccountTransactions
                        .FindAsync(history.CashTransactionId.Value);
                    if (txn != null)
                    {
                        balance.CashBalance -= history.AmountPaidCash;
                        if (balance.CashBalance < 0) balance.CashBalance = 0;
                        db.AccountTransactions.Remove(txn);
                    }
                }

                // ── Reverse Account transaction ──
                if (history.AccountTransactionId.HasValue && history.AmountPaidAccount > 0)
                {
                    var txn = await db.AccountTransactions
                        .FindAsync(history.AccountTransactionId.Value);
                    if (txn != null)
                    {
                        balance.BankBalance -= history.AmountPaidAccount;
                        if (balance.BankBalance < 0) balance.BankBalance = 0;
                        db.AccountTransactions.Remove(txn);
                    }
                }

                // ── Update invoice ──
                inv.AmountPaid = Math.Max(0, inv.AmountPaid - history.AmountPaid);
                inv.AmountPaidCash = Math.Max(0, inv.AmountPaidCash - history.AmountPaidCash);
                inv.AmountPaidAccount = Math.Max(0, inv.AmountPaidAccount - history.AmountPaidAccount);
                inv.RemainingBalance += history.AmountPaid;
                inv.Status = inv.RemainingBalance <= 0 ? "Clear"
                           : inv.AmountPaid == 0 ? "Unpaid"
                                                       : "Partially Paid";

                // ── Delete history record ──
                db.CustomerPaymentHistories.Remove(history);
                await db.SaveChangesAsync();

                // ✅ Issue 1: Recalculate RemainingAfter
                await RecalculateRemainingAfterAsync(db, inv.CustomerInvoiceId);
                await db.SaveChangesAsync();

                WasModified = true; // ✅ Issue 3

                MessageBox.Show("Payment deleted and balances reversed successfully!",
                    "Deleted", MessageBoxButton.OK, MessageBoxImage.Information);

                await LoadAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error deleting payment:\n{ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        // ✅ Issue 1: Fixed RemainingAfter calculation
        // Starting point = netBill - amount paid at invoice creation (not in history)
        // ══════════════════════════════════════════════════════════════════════
        private static async System.Threading.Tasks.Task RecalculateRemainingAfterAsync(
            AppDbContext db, int invoiceId)
        {
            var inv = await db.CustomerInvoices
                .FirstOrDefaultAsync(i => i.CustomerInvoiceId == invoiceId);
            if (inv == null) return;

            var entries = await db.CustomerPaymentHistories
                .Where(h => h.CustomerInvoiceId == invoiceId)
                .OrderBy(h => h.PaymentDate)
                .ThenBy(h => h.Id)
                .ToListAsync();

            // Amount paid at invoice creation = total AmountPaid - sum of all history payments
            decimal historyTotal = entries.Sum(h => h.AmountPaid);
            decimal paidAtCreation = Math.Max(0, inv.AmountPaid - historyTotal);
            decimal running = inv.NetBill - paidAtCreation;

            foreach (var entry in entries)
            {
                running -= entry.AmountPaid;
                entry.RemainingAfter = Math.Max(0, running);
            }
        }
    }

    // ── ViewModel ─────────────────────────────────────────────────────────────
    public class PaymentHistoryRow
    {
        public int RowNumber { get; set; }
        public int Id { get; set; }
        public DateTime PaymentDate { get; set; }
        public decimal AmountPaid { get; set; }
        public decimal AmountPaidCash { get; set; }
        public decimal AmountPaidAccount { get; set; }
        public decimal RemainingAfter { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string ReceivedBy { get; set; } = string.Empty;
        public int? CashTransactionId { get; set; }
        public int? AccountTransactionId { get; set; }
    }
}