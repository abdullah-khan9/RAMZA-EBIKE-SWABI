// Services/VendorCashService.cs
using Microsoft.EntityFrameworkCore;
using Ramza_EBike_Swabi.Data;
using Ramza_EBike_Swabi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ramza_EBike_Swabi.Services
{
    public class VendorCashService
    {
        // ===========================
        // ADD CASH TO VENDOR
        // ===========================
        public async Task<(bool Success, string Message)> AddCashToVendorAsync(
            int vendorId,
            string vendorName,
            string byWhom,
            decimal amount,
            string source,          // "Cash" or "Account"
            DateTime transactionDate,
            string? remarks)
        {
            if (amount <= 0)
                return (false, "Amount must be greater than zero.");
            if (string.IsNullOrWhiteSpace(byWhom))
                return (false, "Please enter By Whom.");

            using var db = new AppDbContext();

            // Step 1: Deduct from Cash or Account balance
            var accountBalance = await db.AccountBalances.FirstOrDefaultAsync();
            if (accountBalance == null)
            {
                accountBalance = new AccountBalance { CashBalance = 0, BankBalance = 0 };
                db.AccountBalances.Add(accountBalance);
                await db.SaveChangesAsync();
            }

            if (source == "Cash")
            {
                if (accountBalance.CashBalance < amount)
                    return (false, $"Insufficient cash balance. Available: PKR {accountBalance.CashBalance:N2}");
                accountBalance.CashBalance -= amount;
            }
            else
            {
                if (accountBalance.BankBalance < amount)
                    return (false, $"Insufficient account balance. Available: PKR {accountBalance.BankBalance:N2}");
                accountBalance.BankBalance -= amount;
            }

            // Step 2: Get or create vendor cash balance row
            var vendorCash = await db.VendorCashBalances
                .FirstOrDefaultAsync(v => v.VendorId == vendorId);
            if (vendorCash == null)
            {
                vendorCash = new VendorCashBalance
                {
                    VendorId = vendorId,
                    VendorName = vendorName,
                    Balance = 0
                };
                db.VendorCashBalances.Add(vendorCash);
                await db.SaveChangesAsync();
            }

            // Step 3: Add full amount to vendor cash
            vendorCash.Balance += amount;

            string ledgerRemarks = $"Cash to Vendor: {vendorName}" +
                                   (string.IsNullOrWhiteSpace(remarks) ? "" : $" | {remarks}");

            db.VendorCashLedger.Add(new VendorCashLedger
            {
                VendorId = vendorId,
                VendorName = vendorName,
                Type = VendorCashType.Added,
                ByWhom = byWhom,
                Amount = amount,
                Source = source,
                TransactionDate = transactionDate,
                Remarks = ledgerRemarks,
                VendorCashBalanceAfter = vendorCash.Balance
            });

            // ✅ Step 4: Correct TransactionType
            // Cash se vendor cash → WithdrawFromCash  (sirf Cash Balance kam)
            // Account se vendor cash → WithdrawFromAccount (sirf Bank Balance kam)
            db.AccountTransactions.Add(new AccountTransaction
            {
                Type = source == "Cash"
                    ? TransactionType.WithdrawFromCash
                    : TransactionType.WithdrawFromAccount,
                ByWhom = byWhom,
                Amount = amount,
                TransactionDate = transactionDate,
                Remarks = ledgerRemarks,
                CashBalanceAfter = accountBalance.CashBalance,
                BankBalanceAfter = accountBalance.BankBalance
            });

            await db.SaveChangesAsync();
            return (true, $"PKR {amount:N2} added to {vendorName}'s cash balance successfully.");
        }

        // ===========================
        // DEDUCT FROM VENDOR CASH
        // ===========================
        public async Task<(bool Success, string Message)> DeductVendorCashAsync(
            int vendorId, decimal amount, string remarks)
        {
            using var db = new AppDbContext();

            var vendorCash = await db.VendorCashBalances
                .FirstOrDefaultAsync(v => v.VendorId == vendorId);

            if (vendorCash == null || vendorCash.Balance < amount)
                return (false, "Insufficient vendor cash balance.");

            vendorCash.Balance -= amount;

            var vendor = await db.Vendor.FindAsync(vendorId);
            db.VendorCashLedger.Add(new VendorCashLedger
            {
                VendorId = vendorId,
                VendorName = vendor?.VendorName ?? string.Empty,
                Type = VendorCashType.Applied,
                ByWhom = vendor?.VendorName ?? "System",
                Amount = amount,
                Source = "VendorCash",
                TransactionDate = DateTime.Now,
                Remarks = remarks,
                VendorCashBalanceAfter = vendorCash.Balance
            });

            await db.SaveChangesAsync();
            return (true, "Deducted from vendor cash balance.");
        }

        // ===========================
        // GET VENDOR CASH BALANCE
        // ===========================
        public async Task<decimal> GetVendorCashBalanceAsync(int vendorId)
        {
            using var db = new AppDbContext();
            var row = await db.VendorCashBalances
                .FirstOrDefaultAsync(v => v.VendorId == vendorId);
            return row?.Balance ?? 0m;
        }

        public async Task<List<VendorCashBalance>> GetAllVendorCashBalancesAsync()
        {
            using var db = new AppDbContext();
            return await db.VendorCashBalances
                .Where(v => v.Balance > 0)
                .OrderBy(v => v.VendorName)
                .ToListAsync();
        }

        public async Task<decimal> GetTotalVendorCashAsync()
        {
            using var db = new AppDbContext();
            if (!await db.VendorCashBalances.AnyAsync()) return 0m;
            return await db.VendorCashBalances.SumAsync(v => v.Balance);
        }

        // ===========================
        // GET LEDGER HISTORY
        // ===========================
        public async Task<List<VendorCashLedger>> GetAllLedgerAsync()
        {
            using var db = new AppDbContext();
            return await db.VendorCashLedger
                .OrderByDescending(l => l.TransactionDate)
                .ToListAsync();
        }

        public async Task<List<VendorCashLedger>> GetRecentLedgerAsync(int count = 20)
        {
            using var db = new AppDbContext();
            return await db.VendorCashLedger
                .OrderByDescending(l => l.TransactionDate)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<VendorCashLedger>> GetLedgerByDateRangeAsync(
            DateTime from, DateTime to)
        {
            using var db = new AppDbContext();
            return await db.VendorCashLedger
                .Where(l => l.TransactionDate.Date >= from.Date &&
                            l.TransactionDate.Date <= to.Date)
                .OrderByDescending(l => l.TransactionDate)
                .ToListAsync();
        }

        public async Task<List<VendorCashLedger>> GetLedgerByVendorAsync(int vendorId)
        {
            using var db = new AppDbContext();
            return await db.VendorCashLedger
                .Where(l => l.VendorId == vendorId)
                .OrderByDescending(l => l.TransactionDate)
                .ToListAsync();
        }
    }
}