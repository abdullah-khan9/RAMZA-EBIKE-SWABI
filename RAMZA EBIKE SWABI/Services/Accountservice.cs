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
                    balance.CashBalance += amount;
                    break;
                case TransactionType.DepositToAccount:
                    balance.BankBalance += amount;
                    break;
                case TransactionType.ConvertCashToAccount:
                    if (balance.CashBalance < amount)
                        return (false, $"Insufficient cash balance. Available: PKR {balance.CashBalance:N2}");
                    balance.CashBalance -= amount;
                    balance.BankBalance += amount;
                    break;
                case TransactionType.CashWithdraw:
                    if (balance.BankBalance < amount)
                        return (false, $"Insufficient account balance. Available: PKR {balance.BankBalance:N2}");
                    balance.BankBalance -= amount;
                    balance.CashBalance += amount;
                    break;
                case TransactionType.WithdrawFromCash:
                    if (balance.CashBalance < amount)
                        return (false, $"Insufficient cash balance. Available: PKR {balance.CashBalance:N2}");
                    balance.CashBalance -= amount;
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
        // Records the full paid amount and stores invoice ref in remarks.
        // ===========================
        public async Task RecordInvoicePaymentAsync(
            decimal amount,
            string customerName,
            string invoiceRef,
            bool isCash)
        {
            if (amount <= 0) return;

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

            db.AccountTransactions.Add(new AccountTransaction
            {
                Type = isCash
                    ? TransactionType.CashDeposit
                    : TransactionType.DepositToAccount,
                ByWhom = customerName,
                Amount = amount,
                TransactionDate = DateTime.Now,
                Remarks = $"{invoiceRef} | Invoice payment ({(isCash ? "Cash" : "Account")})",
                CashBalanceAfter = balance.CashBalance,
                BankBalanceAfter = balance.BankBalance
            });

            await db.SaveChangesAsync();
        }

        // ===========================
        // INVOICE PAYMENT EDIT — records only the DIFFERENCE
        // oldPaid = what was already in the DB
        // newPaid = what user entered now
        // diff    = newPaid - oldPaid  (+ve = customer paid more, -ve = overpaid corrected)
        // Only records a transaction if diff != 0.
        // ===========================
        public async Task RecordInvoicePaymentEditAsync(
            decimal oldPaid,
            decimal newPaid,
            string customerName,
            string invoiceRef,
            bool isCash)
        {
            decimal diff = newPaid - oldPaid;

            // Nothing changed — no transaction needed
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
                // Customer paid MORE — add difference to balance
                if (isCash) balance.CashBalance += diff;
                else balance.BankBalance += diff;
            }
            else
            {
                // Customer paid LESS (correction) — subtract difference
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
                Type = diff > 0
                    ? (isCash ? TransactionType.CashDeposit : TransactionType.DepositToAccount)
                    : TransactionType.WithdrawFromCash,
                ByWhom = customerName,
                Amount = Math.Abs(diff),
                TransactionDate = DateTime.Now,
                Remarks = $"{invoiceRef} | {direction} ({(isCash ? "Cash" : "Account")}) | Was: PKR {oldPaid:N2} → Now: PKR {newPaid:N2}",
                CashBalanceAfter = balance.CashBalance,
                BankBalanceAfter = balance.BankBalance
            });

            await db.SaveChangesAsync();
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

        public async Task ClearTransactionHistoryAsync()
        {
            using var db = new AppDbContext();
            db.AccountTransactions.RemoveRange(db.AccountTransactions);
            await db.SaveChangesAsync();
        }
    }
}