using Microsoft.EntityFrameworkCore;
using Ramza_EBike_Swabi.Data;
using Ramza_EBike_Swabi.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ramza_EBike_Swabi.Services
{
    public class BikeService
    {
        private readonly BikeModelService _bikeModelService = new();

        public async Task<List<Bike>> GetAllBikesAsync(string? search = null)
        {
            using var db = new AppDbContext();
            IQueryable<Bike> query = db.Bikes;
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(b =>
                    b.Model.Contains(search) ||
                    b.Brand.Contains(search) ||
                    b.VendorName.Contains(search));

            return await query.OrderByDescending(b => b.AddedDate).ToListAsync();
        }

        public async Task AddBikeAsync(Bike bike)
        {
            using var db = new AppDbContext();
            db.Bikes.Add(bike);
            await db.SaveChangesAsync();

            // Save shared specs to BikeModels table
            await _bikeModelService.UpsertAsync(new BikeModel
            {
                Model = bike.Model,
                Brand = bike.Brand,
                MotorPower = bike.MotorPower,
                BatteryCapacity = bike.BatteryCapacity,
                Color = bike.Color,
                Warranty = bike.Warranty
            });
        }

        public async Task UpdateBikeAsync(Bike bike)
        {
            using var db = new AppDbContext();
            // 1. Save the bike
            db.Bikes.Update(bike);
            await db.SaveChangesAsync();

            // 2. Sync matching VendorBillItems
            await SyncVendorBillItemsFromBikeAsync(bike, db);

            // 3. Update shared specs in BikeModels table
            await _bikeModelService.UpsertAsync(new BikeModel
            {
                Model = bike.Model,
                Brand = bike.Brand,
                MotorPower = bike.MotorPower,
                BatteryCapacity = bike.BatteryCapacity,
                Color = bike.Color,
                Warranty = bike.Warranty
            });
        }

        /// <summary>
        /// Deletes a bike and returns true if deletion was successful.
        /// </summary>
        public async Task<bool> DeleteBikeAsync(int id)
        {
            using var db = new AppDbContext();
            var bike = await db.Bikes.FindAsync(id);

            if (bike == null)
                return false;

            db.Bikes.Remove(bike);
            await db.SaveChangesAsync();

            return true;
        }

        public async Task<Bike?> GetBikeAsync(int id)
        {
            using var db = new AppDbContext();
            return await db.Bikes.FindAsync(id);
        }

        // ===========================
        // SYNC TO VENDOR BILL ITEMS
        // ===========================
        private async Task SyncVendorBillItemsFromBikeAsync(Bike bike, AppDbContext db)
        {
            List<VendorBillItem> matchedItems = new();
            if (!string.IsNullOrWhiteSpace(bike.ChassisNumber))
            {
                var byChassisItems = await db.VendorBillItem
                    .Where(i => i.ChassisNumber == bike.ChassisNumber)
                    .ToListAsync();
                matchedItems.AddRange(byChassisItems);
            }

            if (!string.IsNullOrWhiteSpace(bike.MotorNumber))
            {
                var alreadyMatchedIds = matchedItems.Select(i => i.Id).ToHashSet();
                var byMotorItems = await db.VendorBillItem
                    .Where(i => i.MotorNumber == bike.MotorNumber &&
                                !alreadyMatchedIds.Contains(i.Id))
                    .ToListAsync();
                matchedItems.AddRange(byMotorItems);
            }

            if (matchedItems.Count == 0) return;

            foreach (var item in matchedItems)
            {
                item.Model = bike.Model;
                item.Brand = bike.Brand;
                item.MotorPower = bike.MotorPower;
                item.BatteryCapacity = bike.BatteryCapacity;
                item.MotorNumber = bike.MotorNumber;
                item.ChassisNumber = bike.ChassisNumber;
                item.Color = bike.Color;
                item.Warranty = bike.Warranty;
            }

            await db.SaveChangesAsync();
        }
    }
}