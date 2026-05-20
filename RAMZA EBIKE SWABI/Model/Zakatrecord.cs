// Models/ZakatRecord.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ramza_EBike_Swabi.Models
{
    /// <summary>
    /// One Zakat record per lunar year. Locked once fully paid.
    /// </summary>
    public class ZakatRecord
    {
        [Key]
        public int Id { get; set; }

        /// <summary>e.g. "1446 AH" or "2025-2026"</summary>
        [Required]
        public string ZakatYear { get; set; } = string.Empty;

        public DateTime CalculationDate { get; set; } = DateTime.Now;

        // ── Zakatable wealth components ──────────────────────────
        [Column(TypeName = "decimal(18,2)")]
        public decimal CashOnHand { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal BankBalance { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal VendorCash { get; set; }          // included per user request

        [Column(TypeName = "decimal(18,2)")]
        public decimal InventoryWorth { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CustomerReceivables { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal VendorDues { get; set; }          // liability — subtracted

        [Column(TypeName = "decimal(18,2)")]
        public decimal NisabThreshold { get; set; } = 170000m;

        // ── Computed ─────────────────────────────────────────────
        [Column(TypeName = "decimal(18,2)")]
        public decimal NetZakatableWealth { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalZakatDue { get; set; }       // = NetZakatableWealth × 2.5%

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalPaid { get; set; } = 0m;

        [Column(TypeName = "decimal(18,2)")]
        public decimal RemainingDue => TotalZakatDue - TotalPaid;

        // ── Status ───────────────────────────────────────────────
        public bool IsLocked { get; set; } = false;
        public DateTime? LockedDate { get; set; }
        public string? Remarks { get; set; }

        // ── Navigation ────────────────────────────────────────────
        public List<ZakatPayment> Payments { get; set; } = new();
    }
}