// Models/VendorCashBalance.cs
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Ramza_EBike_Swabi.Models
{
    /// <summary>
    /// One row per vendor — stores current prepayment/cash balance for that vendor.
    /// </summary>
    public class VendorCashBalance
    {
        [Key]
        public int Id { get; set; }

        public int VendorId { get; set; }

        public string VendorName { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; set; } = 0;
    }
}