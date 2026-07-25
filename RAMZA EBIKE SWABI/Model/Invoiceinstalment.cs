using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ramza_EBike_Swabi.Models
{
    public class InvoiceInstalment
    {
        [Key]
        public int Id { get; set; }

        public int CustomerInvoiceId { get; set; }
        public CustomerInvoice Invoice { get; set; } = null!;

        public int InstalmentNumber { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public DateTime DueDate { get; set; }

        // "Pending", "Paid", "Overdue"
        public string Status { get; set; } = "Pending";

        public DateTime? PaidOn { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PaidAmount { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal PaidCash { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal PaidAccount { get; set; } = 0;

        // "Cash", "Account", "Cash + Account"
        public string? PaymentSource { get; set; }

        public string? Remarks { get; set; }
    }
}