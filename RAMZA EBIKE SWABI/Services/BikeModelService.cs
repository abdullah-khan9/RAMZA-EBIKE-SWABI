// Services/BikeModelService.cs
using Microsoft.EntityFrameworkCore;
using Ramza_EBike_Swabi.Data;
using Ramza_EBike_Swabi.Models;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ramza_EBike_Swabi.Services
{
    public class BikeModelService
    {
        /// <summary>
        /// Returns all BikeModel records ordered by Model name.
        /// </summary>
        public async Task<List<BikeModel>> GetAllAsync()
        {
            using var db = new AppDbContext();
            return await db.BikeModels
                .OrderBy(b => b.Model)
                .ToListAsync();
        }

        /// <summary>
        /// Search BikeModels by partial model or brand name.
        /// Used for autocomplete dropdowns.
        /// </summary>
        public async Task<List<BikeModel>> SearchAsync(string keyword)
        {
            using var db = new AppDbContext();

            if (string.IsNullOrWhiteSpace(keyword))
                return await db.BikeModels.OrderBy(b => b.Model).ToListAsync();

            string kw = keyword.Trim().ToLower();

            return await db.BikeModels
                .Where(b => b.Model.ToLower().Contains(kw) ||
                            b.Brand.ToLower().Contains(kw))
                .OrderBy(b => b.Model)
                .Take(15)
                .ToListAsync();
        }

        /// <summary>
        /// Inserts a new BikeModel. If a record with the same Model+Brand
        /// already exists, updates it instead (upsert).
        /// Called automatically when saving a vendor bill or editing a bike.
        /// </summary>
        public async Task UpsertAsync(BikeModel incoming)
        {
            using var db = new AppDbContext();

            string modelKey = incoming.Model.Trim().ToLower();
            string brandKey = incoming.Brand.Trim().ToLower();

            var existing = await db.BikeModels
                .FirstOrDefaultAsync(b =>
                    b.Model.ToLower() == modelKey &&
                    b.Brand.ToLower() == brandKey);

            if (existing != null)
            {
                // Update shared fields
                existing.MotorPower = incoming.MotorPower;
                existing.BatteryCapacity = incoming.BatteryCapacity;
                existing.Color = incoming.Color;
                existing.Warranty = incoming.Warranty;
            }
            else
            {
                db.BikeModels.Add(new BikeModel
                {
                    Model = incoming.Model.Trim(),
                    Brand = incoming.Brand.Trim(),
                    MotorPower = incoming.MotorPower,
                    BatteryCapacity = incoming.BatteryCapacity,
                    Color = incoming.Color,
                    Warranty = incoming.Warranty
                });
            }

            await db.SaveChangesAsync();
        }

        public async Task DeleteAsync(int id)
        {
            using var db = new AppDbContext();
            var record = await db.BikeModels.FindAsync(id);
            if (record != null)
            {
                db.BikeModels.Remove(record);
                await db.SaveChangesAsync();
            }
        }
    }
}