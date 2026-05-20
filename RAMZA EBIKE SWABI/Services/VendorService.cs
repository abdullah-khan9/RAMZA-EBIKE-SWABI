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

        // ===========================
        // VENDOR CRUD
        // ===========================
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
            return await db.Vendor.Include(v => v.Bills).FirstOrDefaultAsync(v => v.Id == id);
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

        // ===========================
        // BILLS
        // ===========================
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

        // ===========================
        // ADD BILL
        // ===========================
        public async Task<VendorBill> AddBillAsync(VendorBill bill)
        {
            using var db = new AppDbContext();

            var lastBill = await db.VendorBill
                .Where(b => b.VendorId == bill.VendorId)
                .OrderByDescending(b => b.Id)
                .FirstOrDefaultAsync();

            decimal totalPaid = await db.VendorPaymentRecords
                .Where(p => p.VendorId == bill.VendorId)
                .SumAsync(p => (decimal?)p.AmountPaid) ?? 0m;

            decimal baseRemaining = (lastBill?.RemainingBalance ?? 0m) - totalPaid;
            if (baseRemaining < 0) baseRemaining = 0m;

            bill.PreviousBalance = baseRemaining;

            foreach (var item in bill.Items)
                CalculateItem(item);

            bill.TotalAmount = bill.Items.Sum(i => i.TotalWholesalePrice);
            bill.TotalCommission = bill.Items.Sum(i => i.TotalCommissionAmount);
            bill.TotalTaxPaid = bill.Items.Sum(i => i.TaxOnCommissionAmount * i.Qty);
            bill.RemainingBalance = bill.PreviousBalance + bill.TotalAmount - bill.AmountPaid;

            db.VendorBill.Add(bill);
            await db.SaveChangesAsync();

            await SyncBikesFromBillAsync(bill, db, bill.VendorId);
            await UpsertBikeModelsFromBillAsync(bill.Items);

            return bill;
        }

        // ===========================
        // UPDATE BILL
        // ===========================
        public async Task<VendorBill> UpdateBillAsync(VendorBill bill)
        {
            using var db = new AppDbContext();

            var existing = await db.VendorBill
                .Include(b => b.Items)
                .FirstOrDefaultAsync(b => b.Id == bill.Id);

            if (existing == null) throw new Exception("Bill not found");

            db.VendorBillItem.RemoveRange(existing.Items);

            foreach (var item in bill.Items)
                CalculateItem(item);

            existing.AmountPaid = bill.AmountPaid;
            existing.PaymentSource = bill.PaymentSource;
            existing.Remarks = bill.Remarks;
            existing.Items = bill.Items;
            existing.TotalAmount = bill.Items.Sum(i => i.TotalWholesalePrice);
            existing.TotalCommission = bill.Items.Sum(i => i.TotalCommissionAmount);
            existing.TotalTaxPaid = bill.Items.Sum(i => i.TaxOnCommissionAmount * i.Qty);

            await db.SaveChangesAsync();
            await RecalculateVendorBillsAsync(existing.VendorId);
            await SyncBikesFromBillAsync(existing, db, existing.VendorId);
            await UpsertBikeModelsFromBillAsync(existing.Items);

            return existing;
        }

        // ===========================
        // SOFT DELETE + UNDO
        // ===========================
        public async Task SoftDeleteBillAsync(int billId)
        {
            using var db = new AppDbContext();
            var bill = await db.VendorBill.FirstOrDefaultAsync(b => b.Id == billId);
            if (bill == null) return;
            bill.IsDeleted = true;
            await db.SaveChangesAsync();
            await RecalculateVendorBillsAsync(bill.VendorId);
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
            await RecalculateVendorBillsAsync(bill.VendorId);
        }

        // ===========================
        // RECALCULATE bill chain
        // ===========================
        private async Task RecalculateVendorBillsAsync(int vendorId)
        {
            using var db = new AppDbContext();
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

        // ===========================
        // ITEM CALCULATION
        // ===========================
       
        private void CalculateItem(VendorBillItem item)
        {
            decimal qty = item.Qty <= 0 ? 1 : item.Qty;

            if (item.IsFlatPrice)
            {
                // ===== FLAT PRICE MODE =====
                // BillRate = Retail Price
                // FlatPurchasePrice = Purchase Price
                // No percentages

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
                // ===== PERCENTAGE MODE =====
                // BillRate = Retail Price
                // Calculate Purchase Price from percentages

                decimal retailPrice = item.BillRate;

                item.CommissionAmount = retailPrice * item.CommissionPercent / 100m;
                item.TaxOnCommissionAmount = item.CommissionAmount * item.TaxOnCommissionPercent / 100m;
                item.ExtraCommissionAmount = (retailPrice - item.CommissionAmount) * item.ExtraDiscountPercent / 100m;
                item.FinalCommissionAmount = item.CommissionAmount + item.ExtraCommissionAmount;
                item.TotalCommissionAmount = item.FinalCommissionAmount * qty;
                item.TotalWholesalePrice = ((retailPrice - item.FinalCommissionAmount) + item.TaxOnCommissionAmount) * qty;

                item.RetailSalePrice = retailPrice;
                item.FlatPurchasePrice = 0;
            }
        }

        // ===========================
        // AUTO BIKE SYNC
        // ===========================
        // ===========================
        // AUTO BIKE SYNC
        // ===========================
        private async Task SyncBikesFromBillAsync(VendorBill bill, AppDbContext db, int vendorIdOverride)
        {
            var vendor = await db.Vendor.FindAsync(vendorIdOverride);
            string vName = vendor?.VendorName ?? string.Empty;

            foreach (var item in bill.Items)
            {
                if (string.IsNullOrWhiteSpace(item.Model)) continue;

                Bike? existing = null;
                if (!string.IsNullOrWhiteSpace(item.ChassisNumber))
                    existing = await db.Bikes.FirstOrDefaultAsync(b => b.ChassisNumber == item.ChassisNumber);
                if (existing == null && !string.IsNullOrWhiteSpace(item.MotorNumber))
                    existing = await db.Bikes.FirstOrDefaultAsync(b => b.MotorNumber == item.MotorNumber);

                // ===== BIKE PRICE = ALWAYS BillRate (Retail Price) =====
                // In both modes, BillRate represents the retail/sale price
                decimal bikePrice = item.BillRate;

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
                    existing.Price = bikePrice; // Always BillRate (retail price)
                    existing.VendorName = vName;
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
                        Price = bikePrice, // Always BillRate (retail price)
                        VendorName = vName,
                        AddedDate = DateTime.UtcNow
                    });
                }
            }
            await db.SaveChangesAsync();
        }
        // ===========================
        // TRUE REMAINING BALANCE
        // lastBill.RemainingBalance − SUM(VendorPaymentRecords)
        // ===========================
        public async Task<decimal> GetTrueRemainingBalanceAsync(int vendorId)
        {
            using var db = new AppDbContext();

            var lastBill = await db.VendorBill
                .Where(b => b.VendorId == vendorId)
                .OrderByDescending(b => b.Id)
                .FirstOrDefaultAsync();

            decimal billRemaining = lastBill?.RemainingBalance ?? 0m;

            decimal totalPaid = await db.VendorPaymentRecords
                .Where(p => p.VendorId == vendorId)
                .SumAsync(p => (decimal?)p.AmountPaid) ?? 0m;

            decimal result = billRemaining - totalPaid;
            return result < 0 ? 0m : result;
        }

        // ===========================
        // RECORD VENDOR PAYMENT
        // ===========================
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

            var txn = new AccountTransaction
            {
                Type = TransactionType.WithdrawFromCash,
                ByWhom = record.PaidTo,
                Amount = record.AmountPaid,
                TransactionDate = record.PaymentDate,
                Remarks = BuildPaymentRemark(paymentSource, record.VendorName, record.Remarks)
            };

            switch (paymentSource)
            {
                case "Account":
                    balance.BankBalance -= record.AmountPaid;
                    if (balance.BankBalance < 0) balance.BankBalance = 0;
                    break;

                case "VendorCash":
                    var vcBalance = await db.VendorCashBalances
                        .FirstOrDefaultAsync(v => v.VendorId == record.VendorId);
                    if (vcBalance != null)
                    {
                        vcBalance.Balance -= record.AmountPaid;
                        if (vcBalance.Balance < 0) vcBalance.Balance = 0;
                    }
                    db.VendorCashLedger.Add(new VendorCashLedger
                    {
                        VendorId = record.VendorId,
                        VendorName = record.VendorName,
                        Type = VendorCashType.Applied,
                        ByWhom = record.PaidTo,
                        Amount = record.AmountPaid,
                        Source = "VendorCash",
                        VendorCashBalanceAfter = vcBalance?.Balance ?? 0,
                        TransactionDate = record.PaymentDate,
                        Remarks = "Applied to bill balance" +
                                                 (string.IsNullOrWhiteSpace(record.Remarks) ? "" : $" | {record.Remarks}")
                    });
                    break;

                default: // Cash
                    balance.CashBalance -= record.AmountPaid;
                    if (balance.CashBalance < 0) balance.CashBalance = 0;
                    break;
            }

            txn.CashBalanceAfter = balance.CashBalance;
            txn.BankBalanceAfter = balance.BankBalance;

            db.AccountTransactions.Add(txn);
            await db.SaveChangesAsync();

            record.AccountTransactionId = txn.Id;
            db.VendorPaymentRecords.Add(record);
            await db.SaveChangesAsync();
        }

        // ===========================
        // DELETE VENDOR PAYMENT
        // ===========================
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
                    break;

                case "VendorCash":
                    var vcBalance = await db.VendorCashBalances
                        .FirstOrDefaultAsync(v => v.VendorId == record.VendorId);
                    if (vcBalance != null)
                        vcBalance.Balance += record.AmountPaid;

                    db.VendorCashLedger.Add(new VendorCashLedger
                    {
                        VendorId = record.VendorId,
                        VendorName = record.VendorName,
                        Type = VendorCashType.Refunded,
                        ByWhom = record.PaidTo ?? "",
                        Amount = record.AmountPaid,
                        Source = "VendorCash",
                        VendorCashBalanceAfter = vcBalance?.Balance ?? 0,
                        TransactionDate = DateTime.Now,
                        Remarks = $"Reversal — payment #{record.Id} deleted"
                    });
                    break;

                default: // Cash
                    balance.CashBalance += record.AmountPaid;
                    break;
            }

            db.AccountTransactions.Add(new AccountTransaction
            {
                Type = TransactionType.CashDeposit,
                ByWhom = record.VendorName,
                Amount = record.AmountPaid,
                TransactionDate = DateTime.Now,
                Remarks = $"REVERSAL — Vendor payment #{record.Id} deleted | " +
                                  $"Original: {source} to {record.VendorName} on {record.PaymentDate:dd-MMM-yyyy}",
                CashBalanceAfter = balance.CashBalance,
                BankBalanceAfter = balance.BankBalance
            });

            if (record.AccountTransactionId.HasValue)
            {
                var originalTxn = await db.AccountTransactions
                    .FindAsync(record.AccountTransactionId.Value);
                if (originalTxn != null)
                    db.AccountTransactions.Remove(originalTxn);
            }

            db.VendorPaymentRecords.Remove(record);
            await db.SaveChangesAsync();
        }

        // ===========================
        // UPDATE VENDOR PAYMENT
        // ===========================
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
                        break;

                    case "VendorCash":
                        var vcBalance = await db.VendorCashBalances
                            .FirstOrDefaultAsync(v => v.VendorId == existing.VendorId);
                        if (vcBalance != null)
                        {
                            vcBalance.Balance -= delta;
                            if (vcBalance.Balance < 0) vcBalance.Balance = 0;
                        }
                        db.VendorCashLedger.Add(new VendorCashLedger
                        {
                            VendorId = existing.VendorId,
                            VendorName = existing.VendorName,
                            Type = delta > 0 ? VendorCashType.Applied : VendorCashType.Refunded,
                            ByWhom = updated.PaidTo ?? existing.PaidTo,
                            Amount = Math.Abs(delta),
                            Source = "VendorCash",
                            VendorCashBalanceAfter = vcBalance?.Balance ?? 0,
                            TransactionDate = DateTime.Now,
                            Remarks = $"Adjustment — payment #{existing.Id} edited | " +
                                                     $"Was: PKR {oldAmount:N2} → Now: PKR {newAmount:N2}"
                        });
                        break;

                    default: // Cash
                        balance.CashBalance -= delta;
                        if (balance.CashBalance < 0) balance.CashBalance = 0;
                        break;
                }

                db.AccountTransactions.Add(new AccountTransaction
                {
                    Type = delta > 0
                                        ? TransactionType.WithdrawFromCash
                                        : TransactionType.CashDeposit,
                    ByWhom = updated.PaidTo ?? existing.PaidTo,
                    Amount = Math.Abs(delta),
                    TransactionDate = DateTime.Now,
                    Remarks = $"ADJUSTMENT — Vendor payment #{existing.Id} edited | " +
                                      $"{source} | {existing.VendorName} | " +
                                      $"Was: PKR {oldAmount:N2} → Now: PKR {newAmount:N2}",
                    CashBalanceAfter = balance.CashBalance,
                    BankBalanceAfter = balance.BankBalance
                });

                if (existing.AccountTransactionId.HasValue)
                {
                    var originalTxn = await db.AccountTransactions
                        .FindAsync(existing.AccountTransactionId.Value);
                    if (originalTxn != null)
                    {
                        originalTxn.Amount = newAmount;
                        originalTxn.ByWhom = updated.PaidTo ?? existing.PaidTo;
                        originalTxn.TransactionDate = updated.PaymentDate;
                        originalTxn.Remarks = BuildPaymentRemark(source, existing.VendorName, updated.Remarks);
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

        // ===========================
        // GET PAYMENT HISTORY
        // ===========================
        public async Task<List<VendorPaymentRecord>> GetVendorPaymentHistoryAsync(int vendorId)
        {
            using var db = new AppDbContext();
            return await db.VendorPaymentRecords
                .Where(p => p.VendorId == vendorId)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();
        }

        // ===========================
        // HELPERS
        // ===========================
        private static string BuildPaymentRemark(string source, string vendorName, string? remarks)
        {
            string base_ = $"Vendor payment ({source}) to {vendorName}";
            return string.IsNullOrWhiteSpace(remarks) ? base_ : $"{base_} | {remarks}";
        }

        private async Task UpsertBikeModelsFromBillAsync(IEnumerable<VendorBillItem> items)
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
    }
}