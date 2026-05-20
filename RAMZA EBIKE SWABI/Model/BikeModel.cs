// Models/BikeModel.cs
using System.ComponentModel.DataAnnotations;

namespace Ramza_EBike_Swabi.Models
{
    /// <summary>
    /// Stores shared/reusable bike specifications.
    /// When a new bike model is purchased, its common details are saved here
    /// so they can be auto-filled in future purchases without re-typing.
    /// </summary>
    public class BikeModel
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Model { get; set; } = string.Empty;

        public string Brand { get; set; } = string.Empty;
        public string MotorPower { get; set; } = string.Empty;
        public string BatteryCapacity { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Warranty { get; set; } = string.Empty;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}