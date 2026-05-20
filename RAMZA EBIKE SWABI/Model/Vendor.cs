// Models/Vendor.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Ramza_EBike_Swabi.Models
{
    public class Vendor
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string VendorName { get; set; } = string.Empty;

        public string? Phone { get; set; }
        public string? Address { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        public List<VendorBill> Bills { get; set; } = new();
    }
}