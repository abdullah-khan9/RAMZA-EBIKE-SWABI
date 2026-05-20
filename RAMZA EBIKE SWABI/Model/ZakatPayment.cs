// Models/ZakatPayment.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ramza_EBike_Swabi.Models
{
    /// <summary>
    /// One payment instalment toward a ZakatRecord.
    /// </summary>
    public class ZakatPayment
    {
        [Key]
        public int Id { get; set; }

        public int ZakatRecordId { get; set; }
        public ZakatRecord ZakatRecord { get; set; } = null!;

        /// <summary>Recipient — the person/org receiving zakat</summary>
        [Required]
        public string RecipientName { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.Now;

        /// <summary>Person who physically handed over the money</summary>
        public string PaidByName { get; set; } = string.Empty;

        /// <summary>"Cash" or "Account"</summary>
        public string PaymentSource { get; set; } = "Cash";

        public string? Remarks { get; set; }

        /// <summary>Linked AccountTransaction Id for audit trail</summary>
        public int? AccountTransactionId { get; set; }
    }
}