using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ramza_EBike_Swabi.Models
{
    /// <summary>
    /// Records each payment made to a vendor against their outstanding bill balance.
    /// </summary>
    public class VendorPaymentRecord
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int VendorId { get; set; }

        public string VendorName { get; set; } = string.Empty;

        /// <summary>Remaining balance BEFORE this payment was applied.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal BalanceBefore { get; set; }

        /// <summary>Amount paid in this transaction.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountPaid { get; set; }

        /// <summary>Remaining balance AFTER this payment.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal BalanceAfter { get; set; }

        /// <summary>Cash / Account / VendorCash</summary>
        public string PaymentSource { get; set; } = "Cash";

        public string PaidTo { get; set; } = string.Empty;

        public string? Remarks { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.Now;

        /// <summary>
        /// FK to the AccountTransaction row created when this payment was recorded.
        /// Used to reverse / update the account ledger on edit or delete.
        /// Nullable — existing rows without a link still work gracefully.
        /// </summary>
        public int? AccountTransactionId { get; set; }
    }
}