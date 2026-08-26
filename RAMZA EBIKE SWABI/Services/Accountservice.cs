// Services/AccountService.cs
using Microsoft.EntityFrameworkCore;
using Ramza_EBike_Swabi.Data;
using Ramza_EBike_Swabi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ramza_EBike_Swabi.Services
{
    public class AccountService
    {
        // ===========================
        // GET BALANCE
        // ===========================
        public async Task<AccountBalance> GetBalanceAsync()
        {
            using var db = new AppDbContext();
            var balance = await db.AccountBalances.FirstOrDefaultAsync();
            if (balance == null)
            {
                balance = new AccountBalance { CashBalance = 0, BankBalance = 0 };
                db.AccountBalances.Add(balance);
                await db.SaveChangesAsync();
            }
            return balance;
        }

        // ===========================
        // PROCESS MANUAL TRANSACTION
        // ===========================
        public async Task<(bool Success, string Message)> ProcessTransactionAsync(
            TransactionType type,
            decimal amount,
            string byWhom,
            DateTime transactionDate,
            string? remarks)
        {
            if (amount <= 0)
                return (false, "Amount must be greater than zero.");

            if (string.IsNullOrWhiteSpace(byWhom))
                return (false, "Please enter who performed this transaction.");

            using var db = new AppDbContext();

            var balance = await db.AccountBalances.FirstOrDefaultAsync();
            if (balance == null)
            {
                balance = new AccountBalance { CashBalance = 0, BankBalance = 0 };
                db.AccountBalances.Add(balance);
                await db.SaveChangesAsync();
            }

            switch (type)
            {
                case TransactionType.CashDeposit:
                    // External cash → Cash Balance badh jata hai
                    balance.CashBalance += amount;
                    break;

                case TransactionType.DepositToAccount:
                    // External transfer → Bank Balance badh jata hai
                    balance.BankBalance += amount;
                    break;

                case TransactionType.ConvertCashToAccount:
                    // Cash → Account: Cash kam, Account zyada
                    if (balance.CashBalance < amount)
                        return (false, $"Insufficient cash balance. Available: PKR {balance.CashBalance:N2}");
                    balance.CashBalance -= amount;
                    balance.BankBalance += amount;
                    break;

                case TransactionType.CashWithdraw:
                    // Account → Cash: Bank kam, Cash zyada
                    if (balance.BankBalance < amount)
                        return (false, $"Insufficient account balance. Available: PKR {balance.BankBalance:N2}");
                    balance.BankBalance -= amount;
                    balance.CashBalance += amount;
                    break;

                case TransactionType.WithdrawFromCash:
                    // Spend from Cash: sirf Cash kam hota hai
                    if (balance.CashBalance < amount)
                        return (false, $"Insufficient cash balance. Available: PKR {balance.CashBalance:N2}");
                    balance.CashBalance -= amount;
                    break;

                case TransactionType.WithdrawFromAccount:
                    // ✅ NEW — Spend from Account: sirf Bank Balance kam hota hai
                    if (balance.BankBalance < amount)
                        return (false, $"Insufficient account balance. Available: PKR {balance.BankBalance:N2}");
                    balance.BankBalance -= amount;
                    break;
            }

            db.AccountTransactions.Add(new AccountTransaction
            {
                Type = type,
                ByWhom = byWhom.Trim(),
                Amount = amount,
                TransactionDate = transactionDate,
                Remarks = remarks?.Trim(),
                CashBalanceAfter = balance.CashBalance,
                BankBalanceAfter = balance.BankBalance
            });

            await db.SaveChangesAsync();
            return (true, "Transaction recorded successfully.");
        }

        // ===========================
        // INVOICE PAYMENT — NEW INVOICE
        // ===========================
        public async Task<int?> RecordInvoicePaymentAsync(
            decimal amount,
            string customerName,
            string invoiceRef,
            bool isCash)
        {
            if (amount <= 0) return null;

            using var db = new AppDbContext();

            var balance = await db.AccountBalances.FirstOrDefaultAsync();
            if (balance == null)
            {
                balance = new AccountBalance { CashBalance = 0, BankBalance = 0 };
                db.AccountBalances.Add(balance);
                await db.SaveChangesAsync();
            }

            if (isCash)
                balance.CashBalance += amount;
            else
                balance.BankBalance += amount;

            var txn = new AccountTransaction
            {
                Type = isCash
                    ? TransactionType.CashDeposit
                    : TransactionType.DepositToAccount,
                ByWhom = customerName,
                Amount = amount,
                TransactionDate = DateTime.Now,
                Remarks = $"{invoiceRef} | {customerName} | Invoice payment ({(isCash ? "Cash" : "Account")})",
                CashBalanceAfter = balance.CashBalance,
                BankBalanceAfter = balance.BankBalance
            };
            db.AccountTransactions.Add(txn);

            await db.SaveChangesAsync();
            return txn.Id;
        }

        // ===========================
        // INVOICE PAYMENT EDIT
        // ===========================
        public async Task RecordInvoicePaymentEditAsync(
            decimal oldPaid,
            decimal newPaid,
            string customerName,
            string invoiceRef,
            bool isCash)
        {
            decimal diff = newPaid - oldPaid;
            if (diff == 0) return;

            using var db = new AppDbContext();

            var balance = await db.AccountBalances.FirstOrDefaultAsync();
            if (balance == null)
            {
                balance = new AccountBalance { CashBalance = 0, BankBalance = 0 };
                db.AccountBalances.Add(balance);
                await db.SaveChangesAsync();
            }

            if (diff > 0)
            {
                if (isCash) balance.CashBalance += diff;
                else balance.BankBalance += diff;
            }
            else
            {
                decimal absDiff = Math.Abs(diff);
                if (isCash)
                {
                    balance.CashBalance -= absDiff;
                    if (balance.CashBalance < 0) balance.CashBalance = 0;
                }
                else
                {
                    balance.BankBalance -= absDiff;
                    if (balance.BankBalance < 0) balance.BankBalance = 0;
                }
            }

            string direction = diff > 0 ? "Additional payment" : "Payment correction";

            db.AccountTransactions.Add(new AccountTransaction
            {
                // ✅ Cash correction = WithdrawFromCash, Account correction = WithdrawFromAccount
                Type = diff > 0
                    ? (isCash ? TransactionType.CashDeposit : TransactionType.DepositToAccount)
                    : (isCash ? TransactionType.WithdrawFromCash : TransactionType.WithdrawFromAccount),
                ByWhom = customerName,
                Amount = Math.Abs(diff),
                TransactionDate = DateTime.Now,
                Remarks = $"{invoiceRef} | {customerName} | {direction} ({(isCash ? "Cash" : "Account")}) | Was: PKR {oldPaid:N2} → Now: PKR {newPaid:N2}",
                CashBalanceAfter = balance.CashBalance,
                BankBalanceAfter = balance.BankBalance
            });

            await db.SaveChangesAsync();
        }

        // ===========================
        // ✅ NEW — Precise per-payment transaction tracking (update existing / remove if
        // zeroed / create if new) instead of always adding a fresh "correction" row. Used
        // for the invoice's own creation-time payment (GenerateInvoicePage edit), which is
        // linked one-to-one with a specific AccountTransaction via CustomerPaymentHistory —
        // mirrors the exact pattern PaymentHistoryWindow already uses for Due/Instalment
        // payment edits, so all three payment sources behave identically and consistently.
        // Caller supplies its own db context (so this participates in the caller's save),
        // and this method does NOT call SaveChangesAsync itself except when creating a brand
        // new transaction (needed to obtain its generated Id).
        // ===========================
        public async Task<int?> ApplyInvoicePaymentEditAsync(
            AppDbContext db,
            int? existingTransactionId,
            decimal oldAmount,
            decimal newAmount,
            string customerName,
            string invoiceRef,
            bool isCash,
            string noteSuffix = "")
        {
            var balance = await db.AccountBalances.FirstOrDefaultAsync();
            if (balance == null)
            {
                balance = new AccountBalance { CashBalance = 0, BankBalance = 0 };
                db.AccountBalances.Add(balance);
                await db.SaveChangesAsync();
            }

            decimal diff = newAmount - oldAmount;

            // Amount fully removed — delete the transaction, reverse the balance
            if (oldAmount > 0 && newAmount <= 0)
            {
                if (existingTransactionId.HasValue)
                {
                    var oldTxn = await db.AccountTransactions.FindAsync(existingTransactionId.Value);
                    if (oldTxn != null) db.AccountTransactions.Remove(oldTxn);
                }
                if (isCash)
                {
                    balance.CashBalance -= oldAmount;
                    if (balance.CashBalance < 0) balance.CashBalance = 0;
                }
                else
                {
                    balance.BankBalance -= oldAmount;
                    if (balance.BankBalance < 0) balance.BankBalance = 0;
                }
                return null;
            }

            if (diff == 0) return existingTransactionId;

            string remarks = $"{invoiceRef} | {customerName} | Invoice payment ({(isCash ? "Cash" : "Account")})" +
                              (string.IsNullOrWhiteSpace(noteSuffix) ? "" : $" {noteSuffix}");

            if (existingTransactionId.HasValue)
            {
                var txn = await db.AccountTransactions.FindAsync(existingTransactionId.Value);
                if (txn != null)
                {
                    if (isCash)
                    {
                        balance.CashBalance += diff;
                        if (balance.CashBalance < 0) balance.CashBalance = 0;
                    }
                    else
                    {
                        balance.BankBalance += diff;
                        if (balance.BankBalance < 0) balance.BankBalance = 0;
                    }
                    txn.Amount = newAmount;
                    txn.CashBalanceAfter = balance.CashBalance;
                    txn.BankBalanceAfter = balance.BankBalance;
                    txn.Remarks = remarks;
                    return txn.Id;
                }
            }

            // No existing transaction to update — create a fresh one for the new amount
            if (isCash) balance.CashBalance += newAmount;
            else balance.BankBalance += newAmount;

            var newTxn = new AccountTransaction
            {
                Type = isCash ? TransactionType.CashDeposit : TransactionType.DepositToAccount,
                ByWhom = customerName,
                Amount = newAmount,
                TransactionDate = DateTime.Now,
                Remarks = remarks,
                CashBalanceAfter = balance.CashBalance,
                BankBalanceAfter = balance.BankBalance
            };
            db.AccountTransactions.Add(newTxn);
            await db.SaveChangesAsync();
            return newTxn.Id;
        }

        // ===========================
        // GET TRANSACTIONS
        // ===========================
        public async Task<List<AccountTransaction>> GetRecentTransactionsAsync(int count = 10)
        {
            using var db = new AppDbContext();
            return await db.AccountTransactions
                .OrderByDescending(t => t.TransactionDate)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<AccountTransaction>> GetTransactionsByDateRangeAsync(
            DateTime from, DateTime to)
        {
            using var db = new AppDbContext();
            return await db.AccountTransactions
                .Where(t => t.TransactionDate.Date >= from.Date &&
                            t.TransactionDate.Date <= to.Date)
                .OrderByDescending(t => t.TransactionDate)
                .ToListAsync();
        }

        // ✅ Returns the set of AccountTransaction IDs that came from an instalment
        // payment — determined via the proper CustomerPaymentHistory.InstalmentId link
        // (not by guessing from Remarks text), so the Account page's Instalment tab is
        // always accurate even if remarks wording ever changes.
        public async Task<HashSet<int>> GetInstalmentTransactionIdsAsync()
        {
            using var db = new AppDbContext();
            var history = await db.CustomerPaymentHistories
                .Where(h => h.InstalmentId != null)
                .Select(h => new { h.CashTransactionId, h.AccountTransactionId })
                .ToListAsync();

            var ids = new HashSet<int>();
            foreach (var h in history)
            {
                if (h.CashTransactionId.HasValue) ids.Add(h.CashTransactionId.Value);
                if (h.AccountTransactionId.HasValue) ids.Add(h.AccountTransactionId.Value);
            }
            return ids;
        }

        public async Task ClearTransactionHistoryAsync()
        {
            using var db = new AppDbContext();
            db.AccountTransactions.RemoveRange(db.AccountTransactions);
            await db.SaveChangesAsync();
        }
    }
}