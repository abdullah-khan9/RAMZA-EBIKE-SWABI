using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ramza_EBike_Swabi.Models
{
    public class VendorBillItem
    {
        [Key]
        public int Id { get; set; }
        public int VendorBillId { get; set; }
        public VendorBill VendorBill { get; set; } = null!;

        [Required]
        public string Model { get; set; } = string.Empty;

        // ===== NEW: Pricing Mode =====
        public bool IsFlatPrice { get; set; } = false; // false = percentage mode, true = flat price mode

        [Column(TypeName = "decimal(18,2)")]
        public decimal BillRate { get; set; }

        // ===== NEW: Flat Purchase Price & Retail Price =====
        [Column(TypeName = "decimal(18,2)")]
        public decimal FlatPurchasePrice { get; set; } // Used when IsFlatPrice = true

        [Column(TypeName = "decimal(18,2)")]
        public decimal RetailSalePrice { get; set; } // Price for selling to customer (shown in Bike page)

        public int Qty { get; set; }
        public string? DocumentNumber { get; set; }

        // ===== Bike Detail Fields =====
        public string Brand { get; set; } = string.Empty;
        public string MotorPower { get; set; } = string.Empty;
        public string BatteryCapacity { get; set; } = string.Empty;
        public string MotorNumber { get; set; } = string.Empty;
        public string ChassisNumber { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string Warranty { get; set; } = string.Empty;

        // Percent inputs (only used when IsFlatPrice = false)
        [Column(TypeName = "decimal(18,2)")]
        public decimal CommissionPercent { get; set; } = 8;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxOnCommissionPercent { get; set; } = 12;

        [Column(TypeName = "decimal(18,2)")]
        public decimal ExtraDiscountPercent { get; set; } = 2;

        // Derived amounts
        [Column(TypeName = "decimal(18,2)")]
        public decimal CommissionAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TaxOnCommissionAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal FinalCommissionAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal ExtraCommissionAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCommissionAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalWholesalePrice { get; set; }
    }
}