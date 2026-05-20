// Models/VendorIncentive.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ramza_EBike_Swabi.Models
{
    /// <summary>
    /// Where the incentive money goes when received.
    /// Gift = no money added anywhere (just a record).
    /// </summary>
    public enum IncentiveDestination
    {
        Cash,        // Added to Cash Balance
        Account,     // Added to Account/Bank Balance
        VendorCash,  // Added to Vendor Cash Balance (for that vendor)
        Gift         // No financial impact — just a record
    }

    public class VendorIncentive
    {
        [Key]
        public int Id { get; set; }

        public int VendorId { get; set; }

        public string VendorName { get; set; } = string.Empty;

        /// Incentive name / description e.g. "Eid Bonus", "Sales Target Reward"
        public string IncentiveName { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public IncentiveDestination Destination { get; set; } = IncentiveDestination.Cash;

        public DateTime IncentiveDate { get; set; } = DateTime.Now;

        public string? Remarks { get; set; }

        /// When Destination = VendorCash, this links back to the ledger entry
        public int? VendorCashLedgerId { get; set; }

        /// When Destination = Cash or Account, this links to the account transaction
        public int? AccountTransactionId { get; set; }
    }
}