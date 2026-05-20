// Models/ProfitRecord.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ramza_EBike_Swabi.Models
{
    /// <summary>
    /// One row per invoice — stores the complete profit breakdown for that sale.
    /// </summary>
    public class ProfitRecord
    {
        [Key]
        public int Id { get; set; }

        public int CustomerInvoiceId { get; set; }

        public string CustomerName { get; set; } = string.Empty;

        public DateTime SaleDate { get; set; } = DateTime.Now;

        // Summary of bikes sold in this invoice
        public int BikeCount { get; set; }
        public string BikeModels { get; set; } = string.Empty; // e.g. "Hero 70, Ramza 125"

        // Financial totals
        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalSalePrice { get; set; }      // Sum of all (Price × Qty)

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalWholesaleCost { get; set; }  // Sum of wholesale costs from vendor

        [Column(TypeName = "decimal(18,2)")]
        public decimal Discount { get; set; }            // Discount applied on this invoice

        /// <summary>
        /// Profit = (TotalSalePrice - TotalWholesaleCost) - Discount
        /// </summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal Profit { get; set; }

        public string? Remarks { get; set; }
    }
}