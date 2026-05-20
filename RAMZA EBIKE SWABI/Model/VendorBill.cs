using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ramza_EBike_Swabi.Models
{
    public class VendorBill
    {
        [Key]
        public int Id { get; set; }

        public int VendorId { get; set; }
        public Vendor Vendor { get; set; } = null!;

        [Column(TypeName = "decimal(18,2)")]
        public decimal PreviousBalance { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalAmount { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal AmountPaid { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal NetBill { get; private set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal RemainingBalance { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalCommission { get; set; } = 0;

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalTaxPaid { get; set; } = 0;

        public DateTime BillDate { get; set; } = DateTime.UtcNow;

        // ✅ NEW: Remarks shown on bill view & print
        public string? Remarks { get; set; }

        // ✅ NEW: Which source was used to pay (Cash / Account / VendorCash / None)
        public string PaymentSource { get; set; } = "None";

        public List<VendorBillItem> Items { get; set; } = new();
        public bool IsDeleted { get; set; } = false;
        public DateTime? DeletedAt { get; set; }
    }
}