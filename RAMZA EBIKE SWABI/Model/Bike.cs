using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ramza_EBike_Swabi.Models
{
    public class Bike
    {
        [Key]
        public int BikeId { get; set; }

        // Basic Information
        [Required]
        public string Model { get; set; } = string.Empty;

        [Required]
        public string Brand { get; set; } = string.Empty;

        // Technical Specs
        public string MotorPower { get; set; } = string.Empty;
        public string BatteryCapacity { get; set; } = string.Empty;

        // NEW: Identification Numbers
        public string MotorNumber { get; set; } = string.Empty;
        public string ChassisNumber { get; set; } = string.Empty;


        // Additional Details
        public string Warranty { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Weight { get; set; } = string.Empty;

        // Vendor
        public string VendorName { get; set; } = string.Empty;

        // Pricing
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        // Inventory Management
        public int Quantity { get; set; }

        // System
        public DateTime AddedDate { get; set; } = DateTime.UtcNow;
    }
}
