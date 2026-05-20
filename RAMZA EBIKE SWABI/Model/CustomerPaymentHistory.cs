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

        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountPaid { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.Now;

        public string ReceivedBy { get; set; } = string.Empty;

        public string PaymentMethod { get; set; } = "Cash";

        [Column(TypeName = "decimal(18,2)")]
        public decimal RemainingAfter { get; set; }
    }
}