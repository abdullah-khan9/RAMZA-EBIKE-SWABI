// Models/CustomerInvoice.cs
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ramza_EBike_Swabi.Models
{
    public class CustomerInvoice
    {
        [Key]
        public int CustomerInvoiceId { get; set; }
        public int CustomerId { get; set; }
        public Customer Customer { get; set; } = null!;
        public DateTime InvoiceDate { get; set; } = DateTime.Now;

        // ✅ NEW — optional due date set by shopkeeper
        public DateTime? DueDate { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal Discount { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal NetBill { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountPaid { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal RemainingBalance { get; set; }
        public bool WarrantyCardGiven { get; set; }
        public bool VoucherGivenToCustomer { get; set; }
        public bool VoucherIssuedByCompany { get; set; }
        public string Status { get; set; } = "Pending";
        public string PaymentMethod { get; set; } = "Cash";
        public string? Remarks { get; set; }
        public ICollection<CustomerInvoiceItem> Items { get; set; } = new List<CustomerInvoiceItem>();
    }
}