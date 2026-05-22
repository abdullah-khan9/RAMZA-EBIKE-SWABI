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

            // ✅ Remove sold bikes from inventory
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

            return true;
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