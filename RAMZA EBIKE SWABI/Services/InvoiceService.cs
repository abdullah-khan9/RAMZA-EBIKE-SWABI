using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Ramza_EBike_Swabi.Data;
using Ramza_EBike_Swabi.Models;

namespace Ramza_EBike_Swabi.Services
{
    public class InvoiceService
    {
        private readonly ProfitService _profitService = new();

        // ================= ADD =================
        public async Task<bool> AddInvoiceAsync(
    CustomerInvoice invoice,
    List<CustomerInvoiceItem> items)
        {
            using var db = new AppDbContext();

            invoice.Items = items
                .Where(i => !string.IsNullOrWhiteSpace(i.MotorNumber))
                .ToList();

            db.CustomerInvoices.Add(invoice);
            await db.SaveChangesAsync();

            var soldBikeIds = invoice.Items
                .Where(i => i.BikeId.HasValue)
                .Select(i => i.BikeId!.Value)
                .ToList();

            if (soldBikeIds.Count > 0)
            {
                var bikesToRemove = await db.Bikes
                    .Where(b => soldBikeIds.Contains(b.BikeId))
                    .ToListAsync();
                db.Bikes.RemoveRange(bikesToRemove);
                await db.SaveChangesAsync();
            }

            await _profitService.RecordProfitForInvoiceAsync(
                invoice, invoice.Items.ToList(), isEdit: false);

            // ← Incentive bike account remark — SAME db use karo
            await AddIncentiveBikeAccountRemarkAsync(db, invoice, invoice.Items.ToList());

            return true;
        }

        // ================= UPDATE =================
        public async Task<bool> UpdateInvoiceAsync(CustomerInvoice updatedInvoice)
        {
            using var db = new AppDbContext();

            var existing = await db.CustomerInvoices
                .Include(i => i.Items)
                .Include(i => i.Customer)
                .FirstOrDefaultAsync(i =>
                    i.CustomerInvoiceId == updatedInvoice.CustomerInvoiceId);

            if (existing == null) return false;

            existing.Customer.Name = updatedInvoice.Customer.Name;
            existing.Customer.FatherName = updatedInvoice.Customer.FatherName;
            existing.Customer.Contact = updatedInvoice.Customer.Contact;
            existing.Customer.CNIC = updatedInvoice.Customer.CNIC;
            existing.Customer.Address = updatedInvoice.Customer.Address;

            existing.TotalAmount = updatedInvoice.TotalAmount;
            existing.Discount = updatedInvoice.Discount;
            existing.NetBill = updatedInvoice.NetBill;
            existing.AmountPaid = updatedInvoice.AmountPaid;
            existing.AmountPaidCash = updatedInvoice.AmountPaidCash;    // ✅ NEW
            existing.AmountPaidAccount = updatedInvoice.AmountPaidAccount; // ✅ NEW
            existing.RemainingBalance = updatedInvoice.RemainingBalance;
            existing.Status = updatedInvoice.Status;
            existing.PaymentMethod = updatedInvoice.PaymentMethod;
            existing.Remarks = updatedInvoice.Remarks;
            existing.WarrantyCardGiven = updatedInvoice.WarrantyCardGiven;
            existing.VoucherGivenToCustomer = updatedInvoice.VoucherGivenToCustomer;
            existing.VoucherIssuedByCompany = updatedInvoice.VoucherIssuedByCompany;
            existing.DueDate = updatedInvoice.DueDate; // ✅ NEW
            // Collect OLD bike IDs before replacing items
            var oldBikeIds = existing.Items
                .Where(i => i.BikeId.HasValue)
                .Select(i => i.BikeId!.Value)
                .ToList();

            db.CustomerInvoiceItems.RemoveRange(existing.Items);
            existing.Items = updatedInvoice.Items
                .Where(i => !string.IsNullOrWhiteSpace(i.MotorNumber))
                .ToList();

            await db.SaveChangesAsync();

            // New bike IDs after edit
            var newBikeIds = existing.Items
                .Where(i => i.BikeId.HasValue)
                .Select(i => i.BikeId!.Value)
                .ToList();

            // Bikes added in this edit (not in old list) → remove from inventory
            var bikeIdsToRemove = newBikeIds.Except(oldBikeIds).ToList();

            if (bikeIdsToRemove.Count > 0)
            {
                var bikesToRemove = await db.Bikes
                    .Where(b => bikeIdsToRemove.Contains(b.BikeId))
                    .ToListAsync();

                db.Bikes.RemoveRange(bikesToRemove);
                await db.SaveChangesAsync();
            }

            await _profitService.RecordProfitForInvoiceAsync(
                existing, existing.Items.ToList(), isEdit: true);

            await AddIncentiveBikeAccountRemarkAsync(db, existing, existing.Items.ToList());
            return true;
        }

        private async Task AddIncentiveBikeAccountRemarkAsync(
     AppDbContext db,
     CustomerInvoice invoice,
     List<CustomerInvoiceItem> items)
        {
            var incentiveBikes = new List<string>();

            foreach (var item in items.Where(i => !string.IsNullOrWhiteSpace(i.MotorNumber)))
            {
                VendorBillItem? vendorItem = null;

                if (!string.IsNullOrWhiteSpace(item.MotorNumber))
                    vendorItem = await db.VendorBillItem
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(v => v.MotorNumber == item.MotorNumber);

                if (vendorItem == null && !string.IsNullOrWhiteSpace(item.ChassisNumber))
                    vendorItem = await db.VendorBillItem
                        .IgnoreQueryFilters()
                        .FirstOrDefaultAsync(v => v.ChassisNumber == item.ChassisNumber);

                if (vendorItem?.IsIncentiveBike == true)
                    incentiveBikes.Add(item.Model);
            }

            if (incentiveBikes.Count == 0) return;

            // ← Customer name safely lo
            string customerName = invoice.Customer?.Name ?? "";
            if (string.IsNullOrWhiteSpace(customerName))
            {
                var customer = await db.Customers.FindAsync(invoice.CustomerId);
                customerName = customer?.Name ?? "";
            }

            string invoiceRef = $"INV-{invoice.CustomerInvoiceId:D4}";
            string incentiveNote = $" | 🎁 Incentive Bike Sold: {string.Join(", ", incentiveBikes)}";

            var recentTxns = await db.AccountTransactions
                .OrderByDescending(t => t.Id)
                .Take(5)
                .ToListAsync();

            bool anyUpdated = false;

            foreach (var txn in recentTxns)
            {
                if (txn.Remarks != null &&
                    !txn.Remarks.Contains("🎁") &&
                    (txn.Remarks.Contains(customerName) ||
                     txn.Remarks.Contains(invoiceRef)))
                {
                    txn.Remarks += incentiveNote;
                    anyUpdated = true;
                }
            }

            // Agar koi match nahi mila to last transaction update karo
            if (!anyUpdated && recentTxns.Any())
            {
                var last = recentTxns.First();
                if (last.Remarks != null && !last.Remarks.Contains("🎁"))
                    last.Remarks += incentiveNote;
            }

            await db.SaveChangesAsync();
        }
        // ================= GET ALL =================
        public async Task<List<CustomerInvoice>> GetAllInvoicesAsync()
        {
            using var db = new AppDbContext();
            return await db.CustomerInvoices
                .Include(i => i.Customer)
                .Include(i => i.Items)
                .OrderByDescending(i => i.InvoiceDate)
                .ToListAsync();
        }

        // ================= GET OLD PAID AMOUNT =================
        public async Task<decimal> GetOldPaidAmountAsync(int invoiceId)
        {
            using var db = new AppDbContext();
            var inv = await db.CustomerInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.CustomerInvoiceId == invoiceId);
            return inv?.AmountPaid ?? 0m;
        }

        public async Task<string> GetOldPaymentMethodAsync(int invoiceId)
        {
            using var db = new AppDbContext();
            var inv = await db.CustomerInvoices
                .AsNoTracking()
                .FirstOrDefaultAsync(i => i.CustomerInvoiceId == invoiceId);
            return inv?.PaymentMethod ?? "Cash";
        }
    }
}