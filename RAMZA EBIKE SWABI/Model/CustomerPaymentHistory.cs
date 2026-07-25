// Models/CustomerPaymentHistory.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ramza_EBike_Swabi.Models
{
    public class CustomerPaymentHistory
    {
        [Key]
        public int Id { get; set; }

        public int CustomerInvoiceId { get; set; }

        // ✅ Total paid (Cash + Account)
        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountPaid { get; set; }

        // ✅ NEW — alag alag track
        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountPaidCash { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountPaidAccount { get; set; } = 0;

        public DateTime PaymentDate { get; set; } = DateTime.Now;
        public string ReceivedBy { get; set; } = string.Empty;
        public string PaymentMethod { get; set; } = "Cash";

        [Column(TypeName = "decimal(18,2)")]
        public decimal RemainingAfter { get; set; }

        // ✅ NEW — Account transactions ka reference (reversal ke liye)
        public int? CashTransactionId { get; set; }
        public int? AccountTransactionId { get; set; }

        // ✅ NEW — Agar yeh payment kisi instalment ke through hui hai, to us instalment
        // ka reference (edit/delete par sahi instalment update/reverse karne ke liye).
        // Null hoti hai agar payment "Pay Due" (normal) se hui ho.
        public int? InstalmentId { get; set; }
    }
}