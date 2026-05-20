// Services/VendorIncentiveService.cs
using Microsoft.EntityFrameworkCore;
using Ramza_EBike_Swabi.Data;
using Ramza_EBike_Swabi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ramza_EBike_Swabi.Services
{
    public class VendorIncentiveService
    {
        // ===========================
        // ADD INCENTIVE
        // ===========================
        public async Task<(bool Success, string Message)> AddIncentiveAsync(
            int vendorId,
            string vendorName,
            string incentiveName,
            decimal amount,
            IncentiveDestination destination,
            DateTime incentiveDate,
            string? remarks)
        {
            if (amount <= 0)
                return (false, "Amount must be greater than zero.");
            if (string.IsNullOrWhiteSpace(incentiveName))
                return (false, "Please enter an incentive name.");

            using var db = new AppDbContext();

            var incentive = new VendorIncentive
            {
                VendorId = vendorId,
                VendorName = vendorName,
                IncentiveName = incentiveName.Trim(),
                Amount = amount,
                Destination = destination,
                IncentiveDate = incentiveDate,
                Remarks = remarks?.Trim()
            };

            // Apply to balance based on destination
            if (destination == IncentiveDestination.Cash ||
                destination == IncentiveDestination.Account)
            {
                var balance = await db.AccountBalances.FirstOrDefaultAsync();
                if (balance == null)
                {
                    balance = new AccountBalance { CashBalance = 0, BankBalance = 0 };
                    db.AccountBalances.Add(balance);
                    await db.SaveChangesAsync();
                }

                if (destination == IncentiveDestination.Cash)
                    balance.CashBalance += amount;
                else
                    balance.BankBalance += amount;

                var txn = new AccountTransaction
                {
                    Type = destination == IncentiveDestination.Cash
                        ? TransactionType.CashDeposit
                        : TransactionType.DepositToAccount,
                    ByWhom = vendorName,
                    Amount = amount,
                    TransactionDate = incentiveDate,
                    Remarks = $"Incentive from {vendorName}: {incentiveName}{(string.IsNullOrWhiteSpace(remarks) ? "" : " | " + remarks)}",
                    CashBalanceAfter = balance.CashBalance,
                    BankBalanceAfter = balance.BankBalance
                };
                db.AccountTransactions.Add(txn);
                await db.SaveChangesAsync();

                incentive.AccountTransactionId = txn.Id;
            }
            else if (destination == IncentiveDestination.VendorCash)
            {
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

                vendorCash.Balance += amount;

                var ledger = new VendorCashLedger
                {
                    VendorId = vendorId,
                    VendorName = vendorName,
                    Type = VendorCashType.Added,
                    ByWhom = vendorName,
                    Amount = amount,
                    Source = "Incentive",
                    TransactionDate = incentiveDate,
                    Remarks = $"Incentive: {incentiveName}{(string.IsNullOrWhiteSpace(remarks) ? "" : " | " + remarks)}",
                    VendorCashBalanceAfter = vendorCash.Balance
                };
                db.VendorCashLedger.Add(ledger);
                await db.SaveChangesAsync();

                incentive.VendorCashLedgerId = ledger.Id;
            }
            // Gift: no financial entry, just record

            db.VendorIncentives.Add(incentive);
            await db.SaveChangesAsync();

            return (true, "Incentive added successfully.");
        }

        // ===========================
        // UPDATE INCENTIVE
        // ===========================
        public async Task<(bool Success, string Message)> UpdateIncentiveAsync(
            int incentiveId,
            string incentiveName,
            decimal newAmount,
            IncentiveDestination newDestination,
            DateTime incentiveDate,
            string? remarks)
        {
            if (newAmount <= 0)
                return (false, "Amount must be greater than zero.");

            using var db = new AppDbContext();

            var incentive = await db.VendorIncentives.FindAsync(incentiveId);
            if (incentive == null)
                return (false, "Incentive not found.");

            decimal oldAmount = incentive.Amount;
            var oldDestination = incentive.Destination;
            string vendorName = incentive.VendorName;
            int vendorId = incentive.VendorId;

            // Reverse old balance effect
            await ReverseIncentiveBalanceAsync(db, incentive);

            // Update fields
            incentive.IncentiveName = incentiveName.Trim();
            incentive.Amount = newAmount;
            incentive.Destination = newDestination;
            incentive.IncentiveDate = incentiveDate;
            incentive.Remarks = remarks?.Trim();
            incentive.AccountTransactionId = null;
            incentive.VendorCashLedgerId = null;

            // Apply new balance effect
            if (newDestination == IncentiveDestination.Cash ||
                newDestination == IncentiveDestination.Account)
            {
                var balance = await db.AccountBalances.FirstOrDefaultAsync()
                              ?? new AccountBalance { CashBalance = 0, BankBalance = 0 };

                if (newDestination == IncentiveDestination.Cash)
                    balance.CashBalance += newAmount;
                else
                    balance.BankBalance += newAmount;

                var txn = new AccountTransaction
                {
                    Type = newDestination == IncentiveDestination.Cash
                        ? TransactionType.CashDeposit
                        : TransactionType.DepositToAccount,
                    ByWhom = vendorName,
                    Amount = newAmount,
                    TransactionDate = incentiveDate,
                    Remarks = $"Incentive (edited) from {vendorName}: {incentiveName}{(string.IsNullOrWhiteSpace(remarks) ? "" : " | " + remarks)}",
                    CashBalanceAfter = balance.CashBalance,
                    BankBalanceAfter = balance.BankBalance
                };
                db.AccountTransactions.Add(txn);
                await db.SaveChangesAsync();
                incentive.AccountTransactionId = txn.Id;
            }
            else if (newDestination == IncentiveDestination.VendorCash)
            {
                var vendorCash = await db.VendorCashBalances
                    .FirstOrDefaultAsync(v => v.VendorId == vendorId)
                    ?? new VendorCashBalance { VendorId = vendorId, VendorName = vendorName };

                vendorCash.Balance += newAmount;

                var ledger = new VendorCashLedger
                {
                    VendorId = vendorId,
                    VendorName = vendorName,
                    Type = VendorCashType.Added,
                    ByWhom = vendorName,
                    Amount = newAmount,
                    Source = "Incentive",
                    TransactionDate = incentiveDate,
                    Remarks = $"Incentive (edited): {incentiveName}",
                    VendorCashBalanceAfter = vendorCash.Balance
                };
                db.VendorCashLedger.Add(ledger);
                await db.SaveChangesAsync();
                incentive.VendorCashLedgerId = ledger.Id;
            }

            await db.SaveChangesAsync();
            return (true, "Incentive updated successfully.");
        }

        // ===========================
        // DELETE INCENTIVE
        // ===========================
        public async Task<(bool Success, string Message)> DeleteIncentiveAsync(int incentiveId)
        {
            using var db = new AppDbContext();

            var incentive = await db.VendorIncentives.FindAsync(incentiveId);
            if (incentive == null) return (false, "Incentive not found.");

            await ReverseIncentiveBalanceAsync(db, incentive);

            db.VendorIncentives.Remove(incentive);
            await db.SaveChangesAsync();
            return (true, "Incentive deleted and balance reversed.");
        }

        // ===========================
        // REVERSE BALANCE (used by update + delete)
        // ===========================
        private async Task ReverseIncentiveBalanceAsync(AppDbContext db, VendorIncentive incentive)
        {
            if (incentive.Destination == IncentiveDestination.Cash ||
                incentive.Destination == IncentiveDestination.Account)
            {
                var balance = await db.AccountBalances.FirstOrDefaultAsync();
                if (balance != null)
                {
                    if (incentive.Destination == IncentiveDestination.Cash)
                        balance.CashBalance = Math.Max(0, balance.CashBalance - incentive.Amount);
                    else
                        balance.BankBalance = Math.Max(0, balance.BankBalance - incentive.Amount);

                    // Remove old linked transaction
                    if (incentive.AccountTransactionId.HasValue)
                    {
                        var txn = await db.AccountTransactions
                            .FindAsync(incentive.AccountTransactionId.Value);
                        if (txn != null) db.AccountTransactions.Remove(txn);
                    }
                }
            }
            else if (incentive.Destination == IncentiveDestination.VendorCash)
            {
                var vendorCash = await db.VendorCashBalances
                    .FirstOrDefaultAsync(v => v.VendorId == incentive.VendorId);
                if (vendorCash != null)
                    vendorCash.Balance = Math.Max(0, vendorCash.Balance - incentive.Amount);

                if (incentive.VendorCashLedgerId.HasValue)
                {
                    var ledger = await db.VendorCashLedger
                        .FindAsync(incentive.VendorCashLedgerId.Value);
                    if (ledger != null) db.VendorCashLedger.Remove(ledger);
                }
            }

            await db.SaveChangesAsync();
        }

        // ===========================
        // GET
        // ===========================
        public async Task<List<VendorIncentive>> GetAllIncentivesAsync()
        {
            using var db = new AppDbContext();
            return await db.VendorIncentives
                .OrderByDescending(i => i.IncentiveDate)
                .ToListAsync();
        }

        public async Task<List<VendorIncentive>> GetIncentivesByVendorAsync(int vendorId)
        {
            using var db = new AppDbContext();
            return await db.VendorIncentives
                .Where(i => i.VendorId == vendorId)
                .OrderByDescending(i => i.IncentiveDate)
                .ToListAsync();
        }

        public async Task<List<VendorIncentive>> SearchIncentivesAsync(string keyword)
        {
            using var db = new AppDbContext();
            string kw = keyword.ToLower().Trim();
            return await db.VendorIncentives
                .Where(i => i.VendorName.ToLower().Contains(kw) ||
                            i.IncentiveName.ToLower().Contains(kw) ||
                            (i.Remarks != null && i.Remarks.ToLower().Contains(kw)))
                .OrderByDescending(i => i.IncentiveDate)
                .ToListAsync();
        }

        public async Task<VendorIncentive?> GetByIdAsync(int id)
        {
            using var db = new AppDbContext();
            return await db.VendorIncentives.FindAsync(id);
        }

        public async Task<decimal> GetTotalIncentivesAsync()
        {
            using var db = new AppDbContext();
            if (!await db.VendorIncentives.AnyAsync()) return 0m;
            return await db.VendorIncentives
                .Where(i => i.Destination != IncentiveDestination.Gift)
                .SumAsync(i => i.Amount);
        }
    }
}