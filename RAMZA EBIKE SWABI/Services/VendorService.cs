using Microsoft.EntityFrameworkCore;
using Ramza_EBike_Swabi.Data;
using Ramza_EBike_Swabi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ramza_EBike_Swabi.Services
{
    public class VendorService
    {
        private readonly BikeModelService _bikeModelService = new();

        // ════════════════════════════════════════════════════════
        // VENDOR CRUD
        // ════════════════════════════════════════════════════════

        public async Task<List<Vendor>> GetVendorsAsync()
        {
            using var db = new AppDbContext();
            return await db.Vendor.OrderBy(v => v.VendorName).ToListAsync();
        }

        public async Task<List<Vendor>> GetVendorsAsync(string? search)
        {
            using var db = new AppDbContext();
            var query = db.Vendor.AsQueryable();
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.Trim().ToLower();
                query = query.Where(v =>
                    v.VendorName.ToLower().Contains(search) ||
                    (v.Phone != null && v.Phone.ToLower().Contains(search)) ||
                    (v.Address != null && v.Address.ToLower().Contains(search)));
            }
            return await query.OrderBy(v => v.VendorName).ToListAsync();
        }

        public async Task<Vendor?> GetVendorAsync(int id)
        {
            using var db = new AppDbContext();
            return await db.Vendor
                .Include(v => v.Bills)
                .FirstOrDefaultAsync(v => v.Id == id);
        }

        public async Task<Vendor> AddVendorAsync(Vendor vendor)
        {
            using var db = new AppDbContext();
            db.Vendor.Add(vendor);
            await db.SaveChangesAsync();
            return vendor;
        }

        public async Task<Vendor> UpdateVendorAsync(Vendor vendor)
        {
            using var db = new AppDbContext();
            db.Vendor.Update(vendor);
            await db.SaveChangesAsync();
            return vendor;
        }

        public async Task<bool> DeleteVendorAsync(int id)
        {
            using var db = new AppDbContext();
            var vendor = await db.Vendor.FindAsync(id);
            if (vendor == null) return false;
            db.Vendor.Remove(vendor);
            await db.SaveChangesAsync();
            return true;
        }

        // ════════════════════════════════════════════════════════
        // BILLS — READ
        // ════════════════════════════════════════════════════════

        public async Task<List<VendorBill>> GetBillsForVendorAsync(int vendorId)
        {
            using var db = new AppDbContext();
            return await db.VendorBill
                .Where(b => b.VendorId == vendorId)
                .Include(b => b.Items)
                .OrderByDescending(b => b.Id)
                .ToListAsync();
        }

        public async Task<VendorBill?> GetBillByIdAsync(int billId)
        {
            using var db = new AppDbContext();
            return await db.VendorBill
                .Include(b => b.Items)
                .Include(b => b.Vendor)
                .FirstOrDefaultAsync(b => b.Id == billId);
        }

        // ════════════════════════════════════════════════════════
        // REMAINING BALANCE
        // Single source of truth:
        //   last bill RemainingBalance already includes bill-level AmountPaid.
        //   Standalone payments (VendorPaymentRecords) subtract on top.
        // ════════════════════════════════════════════════════════

        public async Task<decimal> GetTrueRemainingBalanceAsync(int vendorId)
        {
            using var db = new AppDbContext();
            return await ComputeRemainingAsync(vendorId, db);
        }

        /// <summary>
        /// Computes true remaining balance using a shared db context (no new context created).
        /// = lastBill.RemainingBalance − SUM(standalone VendorPaymentRecords)
        /// Bill-level AmountPaid is already baked into bill.RemainingBalance via RecalculateChainAsync.
        /// </summary>
        private static async Task<decimal> ComputeRemainingAsync(int vendorId, AppDbContext db)
        {
            var lastBill = await db.VendorBill
                .Where(b => b.VendorId == vendorId)
                .OrderByDescending(b => b.Id)
                .FirstOrDefaultAsync();

            if (lastBill == null) return 0m;

            decimal standalonePayments = await db.VendorPaymentRecords
                .Where(p => p.VendorId == vendorId)
                .SumAsync(p => (decimal?)p.AmountPaid) ?? 0m;

            decimal result = lastBill.RemainingBalance - standalonePayments;
            return result < 0 ? 0m : result;
        }

        // ════════════════════════════════════════════════════════
        // BILL CHAIN RECALCULATE
        // Recalculates PreviousBalance & RemainingBalance for all
        // bills of a vendor in chronological order.
        // Standalone payments are NOT subtracted here — they are
        // subtracted only in ComputeRemainingAsync (display only).
        // ════════════════════════════════════════════════════════

        private static async Task RecalculateChainAsync(int vendorId, AppDbContext db)
        {
            var bills = await db.VendorBill
                .Where(b => b.VendorId == vendorId)
                .OrderBy(b => b.Id)
                .ToListAsync();

            decimal running = 0m;
            foreach (var bill in bills)
            {
                bill.PreviousBalance = running;
                bill.RemainingBalance = running + bill.TotalAmount - bill.AmountPaid;
                running = bill.RemainingBalance;
            }
            await db.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════════════
        // ADD BILL
        // ════════════════════════════════════════════════════════

        public async Task<VendorBill> AddBillAsync(VendorBill bill)
        {
            using var db = new AppDbContext();

            // Previous balance = last bill remaining − standalone payments
            decimal prevBalance = await ComputeRemainingAsync(bill.VendorId, db);
            bill.PreviousBalance = prevBalance;

            foreach (var item in bill.Items)
                CalculateItem(item);

            bill.TotalAmount = bill.Items.Sum(i => i.TotalWholesalePrice);
            bill.TotalCommission = bill.Items.Sum(i => i.TotalCommissionAmount);
            bill.TotalTaxPaid = bill.Items.Sum(i => i.TaxOnCommissionAmount * i.Qty);
            bill.RemainingBalance = prevBalance + bill.TotalAmount - bill.AmountPaid;

            db.VendorBill.Add(bill);
            await db.SaveChangesAsync();

            // Vendor name (for bike sync) — fetch once
            string vName = (await db.Vendor.FindAsync(bill.VendorId))?.VendorName ?? "";

            await SyncBikesFromBillAsync(bill, db, vName);
            await RecordIncentiveBikesAsync(bill, db);
            await UpsertBikeModelsFromBillAsync(bill.Items, db);

            return bill;
        }

        // ════════════════════════════════════════════════════════
        // UPDATE BILL
        // ════════════════════════════════════════════════════════

        public async Task<VendorBill> UpdateBillAsync(VendorBill bill)
        {
            using var db = new AppDbContext();

            var existing = await db.VendorBill
                .Include(b => b.Items)
                .FirstOrDefaultAsync(b => b.Id == bill.Id);

            if (existing == null) throw new Exception("Bill not found");

            // Save old chassis/motor numbers before delete — to detect newly added bikes
            var oldChassis = existing.Items
                .Select(i => i.ChassisNumber?.Trim().ToLower())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToHashSet();
            var oldMotor = existing.Items
                .Select(i => i.MotorNumber?.Trim().ToLower())
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .ToHashSet();

            // Remove old items first then save — avoids EF tracking conflict
            db.VendorBillItem.RemoveRange(existing.Items);
            await db.SaveChangesAsync();

            // Prepare new items
            foreach (var item in bill.Items)
            {
                CalculateItem(item);
                item.Id = 0;              // new insert
                item.VendorBillId = existing.Id;    // link to correct bill
            }

            existing.AmountPaid = bill.AmountPaid;
            existing.PaymentSource = bill.PaymentSource;
            existing.Remarks = bill.Remarks;
            existing.Items = bill.Items;
            existing.TotalAmount = bill.Items.Sum(i => i.TotalWholesalePrice);
            existing.TotalCommission = bill.Items.Sum(i => i.TotalCommissionAmount);
            existing.TotalTaxPaid = bill.Items.Sum(i => i.TaxOnCommissionAmount * i.Qty);

            await db.SaveChangesAsync();

            // Recalculate chain using same context — no tracking conflict
            await RecalculateChainAsync(existing.VendorId, db);

            // Bike sync (edit mode — sold bikes don't come back, new bikes are added)
            string vName = (await db.Vendor.FindAsync(existing.VendorId))?.VendorName ?? "";
            await SyncBikesOnEditAsync(existing, db, vName, oldChassis, oldMotor);

            await UpsertBikeModelsFromBillAsync(existing.Items, db);
            await UpdateIncentiveBikesOnEditAsync(existing, db);

            return existing;
        }

        // ════════════════════════════════════════════════════════
        // SOFT DELETE + RESTORE
        // ════════════════════════════════════════════════════════

        public async Task SoftDeleteBillAsync(int billId)
        {
            using var db = new AppDbContext();
            var bill = await db.VendorBill.FirstOrDefaultAsync(b => b.Id == billId);
            if (bill == null) return;
            bill.IsDeleted = true;
            await db.SaveChangesAsync();
            await RecalculateChainAsync(bill.VendorId, db);
        }

        public async Task RestoreBillAsync(int billId)
        {
            using var db = new AppDbContext();
            var bill = await db.VendorBill
                .IgnoreQueryFilters()
                .FirstOrDefaultAsync(b => b.Id == billId);
            if (bill == null) return;
            bill.IsDeleted = false;
            await db.SaveChangesAsync();
            await RecalculateChainAsync(bill.VendorId, db);
        }

        // ════════════════════════════════════════════════════════
        // RECORD VENDOR PAYMENT
        // Cash → WithdrawFromCash  | Account → WithdrawFromAccount
        // VendorCash → no AccountTransaction (vendor cash ledger only)
        // After recording: last bill's RemainingBalance is NOT changed.
        // GetTrueRemainingBalanceAsync subtracts standalone payments dynamically.
        // ════════════════════════════════════════════════════════

        public async Task RecordVendorPaymentAsync(
            VendorPaymentRecord record,
            string paymentSource,
            AccountService accountService,
            VendorCashService vendorCashService)
        {
            using var db = new AppDbContext();

            var balance = await db.AccountBalances.FirstOrDefaultAsync();
            if (balance == null)
            {
                balance = new AccountBalance { CashBalance = 0, BankBalance = 0 };
                db.AccountBalances.Add(balance);
                await db.SaveChangesAsync();
            }

            AccountTransaction? txn = null;

            switch (paymentSource)
            {
                case "Account":
                    balance.BankBalance -= record.AmountPaid;
                    if (balance.BankBalance < 0) balance.BankBalance = 0;
                    txn = new AccountTransaction
                    {
                        Type = TransactionType.WithdrawFromAccount,
                        ByWhom = record.PaidTo,
                        Amount = record.AmountPaid,
                        TransactionDate = record.PaymentDate,
                        Remarks = BuildPaymentRemark(paymentSource, record.VendorName, record.Remarks),
                        CashBalanceAfter = balance.CashBalance,
                        BankBalanceAfter = balance.BankBalance
                    };
                    break;

                case "VendorCash":
                    var vcBal = await db.VendorCashBalances
                        .FirstOrDefaultAsync(v => v.VendorId == record.VendorId);
                    if (vcBal != null)
                    {
                        vcBal.Balance -= record.AmountPaid;
                        if (vcBal.Balance < 0) vcBal.Balance = 0;
                    }
                    db.VendorCashLedger.Add(new VendorCashLedger
                    {
                        VendorId = record.VendorId,
                        VendorName = record.VendorName,
                        Type = VendorCashType.Applied,
                        ByWhom = record.PaidTo,
                        Amount = record.AmountPaid,
                        Source = "VendorCash",
                        VendorCashBalanceAfter = vcBal?.Balance ?? 0,
                        TransactionDate = record.PaymentDate,
                        Remarks = "Applied to bill balance" +
                                                 (string.IsNullOrWhiteSpace(record.Remarks) ? "" : $" | {record.Remarks}")
                    });
                    break;

                default: // Cash
                    balance.CashBalance -= record.AmountPaid;
                    if (balance.CashBalance < 0) balance.CashBalance = 0;
                    txn = new AccountTransaction
                    {
                        Type = TransactionType.WithdrawFromCash,
                        ByWhom = record.PaidTo,
                        Amount = record.AmountPaid,
                        TransactionDate = record.PaymentDate,
                        Remarks = BuildPaymentRemark(paymentSource, record.VendorName, record.Remarks),
                        CashBalanceAfter = balance.CashBalance,
                        BankBalanceAfter = balance.BankBalance
                    };
                    break;
            }

            if (txn != null)
                db.AccountTransactions.Add(txn);

            await db.SaveChangesAsync();

            if (txn != null)
                record.AccountTransactionId = txn.Id;

            db.VendorPaymentRecords.Add(record);
            await db.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════════════
        // DELETE VENDOR PAYMENT
        // ════════════════════════════════════════════════════════

        public async Task DeleteVendorPaymentAsync(int paymentId)
        {
            using var db = new AppDbContext();

            var record = await db.VendorPaymentRecords.FindAsync(paymentId);
            if (record == null) throw new Exception("Payment record not found.");

            var balance = await db.AccountBalances.FirstOrDefaultAsync();
            if (balance == null) throw new Exception("Account balance record not found.");

            string source = record.PaymentSource ?? "Cash";

            switch (source)
            {
                case "Account":
                    balance.BankBalance += record.AmountPaid;
                    db.AccountTransactions.Add(new AccountTransaction
                    {
                        Type = TransactionType.DepositToAccount,
                        ByWhom = record.VendorName,
                        Amount = record.AmountPaid,
                        TransactionDate = DateTime.Now,
                        Remarks = $"REVERSAL — Vendor payment #{record.Id} deleted | " +
                                            $"Original: Account to {record.VendorName} on {record.PaymentDate:dd-MMM-yyyy}",
                        CashBalanceAfter = balance.CashBalance,
                        BankBalanceAfter = balance.BankBalance
                    });
                    break;

                case "VendorCash":
                    var vcBal = await db.VendorCashBalances
                        .FirstOrDefaultAsync(v => v.VendorId == record.VendorId);
                    if (vcBal != null)
                        vcBal.Balance += record.AmountPaid;
                    db.VendorCashLedger.Add(new VendorCashLedger
                    {
                        VendorId = record.VendorId,
                        VendorName = record.VendorName,
                        Type = VendorCashType.Refunded,
                        ByWhom = record.PaidTo ?? "",
                        Amount = record.AmountPaid,
                        Source = "VendorCash",
                        VendorCashBalanceAfter = vcBal?.Balance ?? 0,
                        TransactionDate = DateTime.Now,
                        Remarks = $"Reversal — payment #{record.Id} deleted"
                    });
                    break;

                default: // Cash
                    balance.CashBalance += record.AmountPaid;
                    db.AccountTransactions.Add(new AccountTransaction
                    {
                        Type = TransactionType.CashDeposit,
                        ByWhom = record.VendorName,
                        Amount = record.AmountPaid,
                        TransactionDate = DateTime.Now,
                        Remarks = $"REVERSAL — Vendor payment #{record.Id} deleted | " +
                                            $"Original: Cash to {record.VendorName} on {record.PaymentDate:dd-MMM-yyyy}",
                        CashBalanceAfter = balance.CashBalance,
                        BankBalanceAfter = balance.BankBalance
                    });
                    break;
            }

            // Remove original AccountTransaction if linked
            if (record.AccountTransactionId.HasValue)
            {
                var origTxn = await db.AccountTransactions
                    .FindAsync(record.AccountTransactionId.Value);
                if (origTxn != null)
                    db.AccountTransactions.Remove(origTxn);
            }

            db.VendorPaymentRecords.Remove(record);
            await db.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════════════
        // UPDATE VENDOR PAYMENT
        // ════════════════════════════════════════════════════════

        public async Task UpdateVendorPaymentAsync(VendorPaymentRecord updated)
        {
            using var db = new AppDbContext();

            var existing = await db.VendorPaymentRecords.FindAsync(updated.Id);
            if (existing == null) throw new Exception("Payment record not found.");

            var balance = await db.AccountBalances.FirstOrDefaultAsync();
            if (balance == null) throw new Exception("Account balance record not found.");

            decimal oldAmount = existing.AmountPaid;
            decimal newAmount = updated.AmountPaid;
            decimal delta = newAmount - oldAmount;

            string source = updated.PaymentSource ?? existing.PaymentSource ?? "Cash";

            if (delta != 0)
            {
                switch (source)
                {
                    case "Account":
                        balance.BankBalance -= delta;
                        if (balance.BankBalance < 0) balance.BankBalance = 0;
                        db.AccountTransactions.Add(new AccountTransaction
                        {
                            Type = delta > 0 ? TransactionType.WithdrawFromAccount : TransactionType.DepositToAccount,
                            ByWhom = updated.PaidTo ?? existing.PaidTo,
                            Amount = Math.Abs(delta),
                            TransactionDate = DateTime.Now,
                            Remarks = $"ADJUSTMENT — Vendor payment #{existing.Id} edited | " +
                                                $"Account | {existing.VendorName} | " +
                                                $"Was: PKR {oldAmount:N2} → Now: PKR {newAmount:N2}",
                            CashBalanceAfter = balance.CashBalance,
                            BankBalanceAfter = balance.BankBalance
                        });
                        break;

                    case "VendorCash":
                        var vcBal = await db.VendorCashBalances
                            .FirstOrDefaultAsync(v => v.VendorId == existing.VendorId);
                        if (vcBal != null)
                        {
                            vcBal.Balance -= delta;
                            if (vcBal.Balance < 0) vcBal.Balance = 0;
                        }
                        db.VendorCashLedger.Add(new VendorCashLedger
                        {
                            VendorId = existing.VendorId,
                            VendorName = existing.VendorName,
                            Type = delta > 0 ? VendorCashType.Applied : VendorCashType.Refunded,
                            ByWhom = updated.PaidTo ?? existing.PaidTo,
                            Amount = Math.Abs(delta),
                            Source = "VendorCash",
                            VendorCashBalanceAfter = vcBal?.Balance ?? 0,
                            TransactionDate = DateTime.Now,
                            Remarks = $"Adjustment — payment #{existing.Id} edited | " +
                                                     $"Was: PKR {oldAmount:N2} → Now: PKR {newAmount:N2}"
                        });
                        break;

                    default: // Cash
                        balance.CashBalance -= delta;
                        if (balance.CashBalance < 0) balance.CashBalance = 0;
                        db.AccountTransactions.Add(new AccountTransaction
                        {
                            Type = delta > 0 ? TransactionType.WithdrawFromCash : TransactionType.CashDeposit,
                            ByWhom = updated.PaidTo ?? existing.PaidTo,
                            Amount = Math.Abs(delta),
                            TransactionDate = DateTime.Now,
                            Remarks = $"ADJUSTMENT — Vendor payment #{existing.Id} edited | " +
                                                $"Cash | {existing.VendorName} | " +
                                                $"Was: PKR {oldAmount:N2} → Now: PKR {newAmount:N2}",
                            CashBalanceAfter = balance.CashBalance,
                            BankBalanceAfter = balance.BankBalance
                        });
                        break;
                }

                // Update original transaction amount if linked
                if (existing.AccountTransactionId.HasValue)
                {
                    var origTxn = await db.AccountTransactions
                        .FindAsync(existing.AccountTransactionId.Value);
                    if (origTxn != null)
                    {
                        origTxn.Amount = newAmount;
                        origTxn.ByWhom = updated.PaidTo ?? existing.PaidTo;
                        origTxn.TransactionDate = updated.PaymentDate;
                        origTxn.Remarks = BuildPaymentRemark(source, existing.VendorName, updated.Remarks);
                    }
                }
            }

            existing.AmountPaid = newAmount;
            existing.PaymentSource = source;
            existing.PaidTo = updated.PaidTo;
            existing.Remarks = updated.Remarks;
            existing.PaymentDate = updated.PaymentDate;
            existing.BalanceAfter = existing.BalanceBefore - newAmount;

            await db.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════════════
        // GET PAYMENT HISTORY
        // ════════════════════════════════════════════════════════

        public async Task<List<VendorPaymentRecord>> GetVendorPaymentHistoryAsync(int vendorId)
        {
            using var db = new AppDbContext();
            return await db.VendorPaymentRecords
                .Where(p => p.VendorId == vendorId)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();
        }

        // ════════════════════════════════════════════════════════
        // ITEM CALCULATION (pure — no DB calls)
        // ════════════════════════════════════════════════════════

        private static void CalculateItem(VendorBillItem item)
        {
            decimal qty = item.Qty <= 0 ? 1 : item.Qty;

            if (item.IsIncentiveBike)
            {
                item.IsFlatPrice = true;
                item.FlatPurchasePrice = 0;
                item.CommissionAmount = 0;
                item.TaxOnCommissionAmount = 0;
                item.ExtraCommissionAmount = 0;
                item.FinalCommissionAmount = 0;
                item.TotalCommissionAmount = 0;
                item.TotalWholesalePrice = 0;
                item.RetailSalePrice = item.BillRate;
            }
            else if (item.IsFlatPrice)
            {
                item.CommissionAmount = 0;
                item.TaxOnCommissionAmount = 0;
                item.ExtraCommissionAmount = 0;
                item.FinalCommissionAmount = 0;
                item.TotalCommissionAmount = 0;
                item.TotalWholesalePrice = item.FlatPurchasePrice * qty;
                item.RetailSalePrice = item.BillRate;
            }
            else
            {
                decimal rp = item.BillRate;
                item.CommissionAmount = rp * item.CommissionPercent / 100m;
                item.TaxOnCommissionAmount = item.CommissionAmount * item.TaxOnCommissionPercent / 100m;
                item.ExtraCommissionAmount = (rp - item.CommissionAmount) * item.ExtraDiscountPercent / 100m;
                item.FinalCommissionAmount = item.CommissionAmount + item.ExtraCommissionAmount;
                item.TotalCommissionAmount = item.FinalCommissionAmount * qty;
                item.TotalWholesalePrice = ((rp - item.FinalCommissionAmount) + item.TaxOnCommissionAmount) * qty;
                item.RetailSalePrice = rp;
                item.FlatPurchasePrice = 0;
            }
        }

        // ════════════════════════════════════════════════════════
        // BIKE SYNC — ADD BILL
        // Batch: fetch all existing bikes in ONE query instead of N queries
        // ════════════════════════════════════════════════════════

        private static async Task SyncBikesFromBillAsync(VendorBill bill, AppDbContext db, string vName)
        {
            var items = bill.Items
                .Where(i => !string.IsNullOrWhiteSpace(i.Model))
                .ToList();
            if (!items.Any()) return;

            // Collect all chassis/motor numbers in one go
            var chassisNums = items
                .Where(i => !string.IsNullOrWhiteSpace(i.ChassisNumber))
                .Select(i => i.ChassisNumber!)
                .ToList();
            var motorNums = items
                .Where(i => !string.IsNullOrWhiteSpace(i.MotorNumber))
                .Select(i => i.MotorNumber!)
                .ToList();

            // Single batch query
            var existingBikes = await db.Bikes
                .Where(b => chassisNums.Contains(b.ChassisNumber!) || motorNums.Contains(b.MotorNumber!))
                .ToListAsync();

            foreach (var item in items)
            {
                var existing = existingBikes.FirstOrDefault(b =>
                    (!string.IsNullOrWhiteSpace(item.ChassisNumber) && b.ChassisNumber == item.ChassisNumber) ||
                    (!string.IsNullOrWhiteSpace(item.MotorNumber) && b.MotorNumber == item.MotorNumber));

                if (existing != null)
                {
                    existing.Model = item.Model;
                    existing.Brand = item.Brand;
                    existing.MotorPower = item.MotorPower;
                    existing.BatteryCapacity = item.BatteryCapacity;
                    existing.MotorNumber = item.MotorNumber;
                    existing.ChassisNumber = item.ChassisNumber;
                    existing.Color = item.Color;
                    existing.Warranty = item.Warranty;
                    existing.Quantity = item.Qty;
                    existing.Price = item.BillRate;
                    existing.VendorName = vName;
                    existing.IsIncentiveBike = item.IsIncentiveBike;
                }
                else
                {
                    db.Bikes.Add(new Bike
                    {
                        Model = item.Model,
                        Brand = item.Brand,
                        MotorPower = item.MotorPower,
                        BatteryCapacity = item.BatteryCapacity,
                        MotorNumber = item.MotorNumber,
                        ChassisNumber = item.ChassisNumber,
                        Color = item.Color,
                        Warranty = item.Warranty,
                        Quantity = item.Qty,
                        Price = item.BillRate,
                        VendorName = vName,
                        AddedDate = DateTime.UtcNow,
                        IsIncentiveBike = item.IsIncentiveBike
                    });
                }
            }
            await db.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════════════
        // BIKE SYNC — EDIT BILL
        // existing bikes → update only
        // sold bikes (old item, not in Bikes table) → ignore
        // new bikes (not in oldChassis/oldMotor) → add
        // ════════════════════════════════════════════════════════

        private static async Task SyncBikesOnEditAsync(
            VendorBill bill,
            AppDbContext db,
            string vName,
            HashSet<string?> oldChassis,
            HashSet<string?> oldMotor)
        {
            var items = bill.Items
                .Where(i => !string.IsNullOrWhiteSpace(i.Model))
                .ToList();
            if (!items.Any()) return;

            var chassisNums = items
                .Where(i => !string.IsNullOrWhiteSpace(i.ChassisNumber))
                .Select(i => i.ChassisNumber!)
                .ToList();
            var motorNums = items
                .Where(i => !string.IsNullOrWhiteSpace(i.MotorNumber))
                .Select(i => i.MotorNumber!)
                .ToList();

            // Single batch query
            var existingBikes = await db.Bikes
                .Where(b => chassisNums.Contains(b.ChassisNumber!) || motorNums.Contains(b.MotorNumber!))
                .ToListAsync();

            foreach (var item in items)
            {
                var existing = existingBikes.FirstOrDefault(b =>
                    (!string.IsNullOrWhiteSpace(item.ChassisNumber) && b.ChassisNumber == item.ChassisNumber) ||
                    (!string.IsNullOrWhiteSpace(item.MotorNumber) && b.MotorNumber == item.MotorNumber));

                if (existing != null)
                {
                    // Bike table mein hai — update
                    existing.Model = item.Model;
                    existing.Brand = item.Brand;
                    existing.MotorPower = item.MotorPower;
                    existing.BatteryCapacity = item.BatteryCapacity;
                    existing.MotorNumber = item.MotorNumber;
                    existing.ChassisNumber = item.ChassisNumber;
                    existing.Color = item.Color;
                    existing.Warranty = item.Warranty;
                    existing.Quantity = item.Qty;
                    existing.Price = item.BillRate;
                    existing.VendorName = vName;
                    existing.IsIncentiveBike = item.IsIncentiveBike;
                }
                else
                {
                    // Bike table mein nahi mili
                    bool wasOldItem =
                        (!string.IsNullOrWhiteSpace(item.ChassisNumber) &&
                         oldChassis.Contains(item.ChassisNumber.Trim().ToLower())) ||
                        (!string.IsNullOrWhiteSpace(item.MotorNumber) &&
                         oldMotor.Contains(item.MotorNumber.Trim().ToLower()));

                    if (!wasOldItem)
                    {
                        // Nai bike — add karo
                        db.Bikes.Add(new Bike
                        {
                            Model = item.Model,
                            Brand = item.Brand,
                            MotorPower = item.MotorPower,
                            BatteryCapacity = item.BatteryCapacity,
                            MotorNumber = item.MotorNumber,
                            ChassisNumber = item.ChassisNumber,
                            Color = item.Color,
                            Warranty = item.Warranty,
                            Quantity = item.Qty,
                            Price = item.BillRate,
                            VendorName = vName,
                            AddedDate = DateTime.UtcNow,
                            IsIncentiveBike = item.IsIncentiveBike
                        });
                    }
                    // else: purani item thi, sold ho gayi — ignore
                }
            }
            await db.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════════════
        // INCENTIVE BIKES — RECORD ON ADD
        // ════════════════════════════════════════════════════════

        private static async Task RecordIncentiveBikesAsync(VendorBill bill, AppDbContext db)
        {
            var incentiveItems = bill.Items
                .Where(i => i.IsIncentiveBike && !string.IsNullOrWhiteSpace(i.Model))
                .ToList();
            if (!incentiveItems.Any()) return;

            var vendor = await db.Vendor.FindAsync(bill.VendorId);
            string vendorName = vendor?.VendorName ?? "";

            foreach (var item in incentiveItems)
            {
                db.VendorIncentives.Add(new VendorIncentive
                {
                    VendorId = bill.VendorId,
                    VendorName = vendorName,
                    IncentiveName = $"Bike Gift — {item.Model}",
                    Amount = item.BillRate * item.Qty,
                    Destination = IncentiveDestination.Gift,
                    IncentiveDate = bill.BillDate,
                    Remarks = $"Incentive bike from bill #{bill.Id} | " +
                                     $"Model: {item.Model} | " +
                                     $"Chassis: {item.ChassisNumber} | " +
                                     $"Motor: {item.MotorNumber} | " +
                                     $"Retail Value: PKR {item.BillRate * item.Qty:N2}"
                });
            }
            await db.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════════════
        // INCENTIVE BIKES — UPDATE ON EDIT
        // ════════════════════════════════════════════════════════

        private static async Task UpdateIncentiveBikesOnEditAsync(VendorBill bill, AppDbContext db)
        {
            var vendor = await db.Vendor.FindAsync(bill.VendorId);
            string vendorName = vendor?.VendorName ?? "";

            var oldIncentives = await db.VendorIncentives
                .Where(i => i.Remarks != null &&
                            i.Remarks.Contains($"bill #{bill.Id}") &&
                            i.Destination == IncentiveDestination.Gift)
                .ToListAsync();

            db.VendorIncentives.RemoveRange(oldIncentives);
            await db.SaveChangesAsync();

            var incentiveItems = bill.Items
                .Where(i => i.IsIncentiveBike && !string.IsNullOrWhiteSpace(i.Model))
                .ToList();

            foreach (var item in incentiveItems)
            {
                db.VendorIncentives.Add(new VendorIncentive
                {
                    VendorId = bill.VendorId,
                    VendorName = vendorName,
                    IncentiveName = $"Bike Gift — {item.Model}",
                    Amount = item.BillRate * item.Qty,
                    Destination = IncentiveDestination.Gift,
                    IncentiveDate = bill.BillDate,
                    Remarks = $"Incentive bike from bill #{bill.Id} | " +
                                     $"Model: {item.Model} | " +
                                     $"Chassis: {item.ChassisNumber} | " +
                                     $"Motor: {item.MotorNumber} | " +
                                     $"Retail Value: PKR {item.BillRate * item.Qty:N2}"
                });
            }
            await db.SaveChangesAsync();
        }

        // ════════════════════════════════════════════════════════
        // BIKE MODELS UPSERT
        // Batch: single db context, all items in one pass
        // ════════════════════════════════════════════════════════

        private async Task UpsertBikeModelsFromBillAsync(IEnumerable<VendorBillItem> items, AppDbContext db)
        {
            foreach (var item in items)
            {
                if (string.IsNullOrWhiteSpace(item.Model)) continue;
                await _bikeModelService.UpsertAsync(new BikeModel
                {
                    Model = item.Model,
                    Brand = item.Brand,
                    MotorPower = item.MotorPower,
                    BatteryCapacity = item.BatteryCapacity,
                    Color = item.Color,
                    Warranty = item.Warranty
                });
            }
        }

        // ════════════════════════════════════════════════════════
        // HELPERS
        // ════════════════════════════════════════════════════════

        private static string BuildPaymentRemark(string source, string vendorName, string? remarks)
        {
            string base_ = $"Vendor payment ({source}) to {vendorName}";
            return string.IsNullOrWhiteSpace(remarks) ? base_ : $"{base_} | {remarks}";
        }
    }
}