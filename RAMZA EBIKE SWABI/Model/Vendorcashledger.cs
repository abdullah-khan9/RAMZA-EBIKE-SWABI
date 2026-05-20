// Models/VendorCashLedger.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ramza_EBike_Swabi.Models
{
    public enum VendorCashType
    {
        Added,    // Cash added to vendor (deducted from Cash/Account balance)
        Applied,  // Applied against vendor remaining balance
        Refunded  // Excess returned to cash balance
    }

    public class VendorCashLedger
    {
        [Key]
        public int Id { get; set; }

        public int VendorId { get; set; }
        public string VendorName { get; set; } = string.Empty;

        public VendorCashType Type { get; set; }

        public string ByWhom { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        /// "Cash" or "Account"
        public string Source { get; set; } = "Cash";

        public DateTime TransactionDate { get; set; } = DateTime.Now;

        public string? Remarks { get; set; }

        // Running balance for this vendor after this transaction
        [Column(TypeName = "decimal(18,2)")]
        public decimal VendorCashBalanceAfter { get; set; }
    }
}