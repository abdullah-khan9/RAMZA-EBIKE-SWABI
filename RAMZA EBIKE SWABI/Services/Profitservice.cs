// Services/ProfitService.cs
using Microsoft.EntityFrameworkCore;
using Ramza_EBike_Swabi.Data;
using Ramza_EBike_Swabi.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ramza_EBike_Swabi.Services
{
    public class ProfitService
    {
        /// <summary>
        /// Records ONE profit row per invoice.
        ///
        /// Formula:
        ///   TotalSalePrice    = Sum of (item.Price × item.Qty) for all items
        ///   TotalWholesaleCost= Sum of vendorBillItem.TotalWholesalePrice for matching items
        ///   Profit            = (TotalSalePrice - TotalWholesaleCost) - Discount
        ///
        /// On EDIT: the existing profit row for this invoice is DELETED first,
        /// then a fresh one is inserted — no doubling, always correct.
        /// </summary>
        public async Task RecordProfitForInvoiceAsync(
            CustomerInvoice invoice,
            List<CustomerInvoiceItem> soldItems,
            bool isEdit = false)
        {
            if (soldItems == null || soldItems.Count == 0) return;

            using var db = new AppDbContext();

            // Always delete existing profit row for this invoice first
            var existing = db.ProfitRecords
                .Where(p => p.CustomerInvoiceId == invoice.CustomerInvoiceId);
            db.ProfitRecords.RemoveRange(existing);
            await db.SaveChangesAsync();

            var validItems = soldItems
                .Where(i => !string.IsNullOrWhiteSpace(i.MotorNumber))
                .ToList();

            if (validItems.Count == 0) return;

            // Sum all sale prices
            decimal totalSalePrice = validItems.Sum(i => i.Price * i.Quantity);

            // Sum all wholesale costs by matching each item in vendor records
            decimal totalWholesaleCost = 0m;
            var missingWholesale = new List<string>();

            foreach (var item in validItems)
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

                if (vendorItem != null)
                    totalWholesaleCost += vendorItem.TotalWholesalePrice;
                else
                    missingWholesale.Add(item.Model);
            }

            decimal discount = invoice.Discount;
            decimal profit = (totalSalePrice - totalWholesaleCost) - discount;

            string bikeModels = string.Join(", ", validItems
                .Select(i => string.IsNullOrWhiteSpace(i.Model) ? "Unknown" : i.Model)
                .Distinct());

            string? remarks = null;
            if (missingWholesale.Count > 0)
                remarks = $"Wholesale cost not found for: {string.Join(", ", missingWholesale)}";

            db.ProfitRecords.Add(new ProfitRecord
            {
                CustomerInvoiceId = invoice.CustomerInvoiceId,
                CustomerName = invoice.Customer?.Name ?? string.Empty,
                SaleDate = invoice.InvoiceDate,
                BikeCount = validItems.Count,
                BikeModels = bikeModels,
                TotalSalePrice = totalSalePrice,
                TotalWholesaleCost = totalWholesaleCost,
                Discount = discount,
                Profit = profit,
                Remarks = remarks
            });

            await db.SaveChangesAsync();
        }

        public async Task<decimal> GetTotalProfitAsync()
        {
            using var db = new AppDbContext();
            if (!await db.ProfitRecords.AnyAsync()) return 0m;
            return await db.ProfitRecords.SumAsync(p => p.Profit);
        }

        public async Task<List<ProfitRecord>> GetRecentProfitRecordsAsync(int count = 10)
        {
            using var db = new AppDbContext();
            return await db.ProfitRecords
                .OrderByDescending(p => p.SaleDate)
                .Take(count)
                .ToListAsync();
        }

        public async Task<List<ProfitRecord>> GetProfitByDateRangeAsync(
            DateTime from, DateTime to)
        {
            using var db = new AppDbContext();
            return await db.ProfitRecords
                .Where(p => p.SaleDate.Date >= from.Date &&
                            p.SaleDate.Date <= to.Date)
                .OrderByDescending(p => p.SaleDate)
                .ToListAsync();
        }

        public async Task ClearProfitHistoryAsync()
        {
            using var db = new AppDbContext();
            db.ProfitRecords.RemoveRange(db.ProfitRecords);
            await db.SaveChangesAsync();
        }
    }
}