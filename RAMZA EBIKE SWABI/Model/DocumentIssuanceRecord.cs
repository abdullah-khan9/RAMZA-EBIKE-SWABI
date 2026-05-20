// Models/DocumentIssuanceRecord.cs
using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ramza_EBike_Swabi.Models
{
    /// <summary>
    /// Records each time a physical document (warranty card / voucher) is
    /// handed over to a customer AFTER the invoice was originally created.
    /// </summary>
    public class DocumentIssuanceRecord
    {
        [Key]
        public int DocumentIssuanceId { get; set; }

        /// <summary>FK to CustomerInvoice.</summary>
        public int CustomerInvoiceId { get; set; }

        [ForeignKey(nameof(CustomerInvoiceId))]
        public CustomerInvoice? Invoice { get; set; }

        public bool WarrantyCardIssued { get; set; }
        public bool VoucherCustomerIssued { get; set; }
        public bool VoucherCompanyIssued { get; set; }

        [Required, MaxLength(200)]
        public string IssuedBy { get; set; } = string.Empty;

        [Required, MaxLength(200)]
        public string ReceivedBy { get; set; } = string.Empty;

        public DateTime IssuanceDate { get; set; } = DateTime.Now;

        [MaxLength(500)]
        public string? Notes { get; set; }
    }
}