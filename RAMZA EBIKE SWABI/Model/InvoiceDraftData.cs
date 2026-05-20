// Models/InvoiceDraftData.cs
using System.Collections.Generic;

namespace Ramza_EBike_Swabi.Models
{
    /// <summary>
    /// Plain data class that holds everything typed into one invoice tab.
    /// Serialized to JSON and stored in InvoiceDraft.DraftJson.
    /// </summary>
    public class InvoiceDraftData
    {
        // Customer fields
        public string Name { get; set; } = string.Empty;
        public string FatherName { get; set; } = string.Empty;
        public string Contact { get; set; } = string.Empty;
        public string CNIC { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;

        // Financial
        public string Discount { get; set; } = string.Empty;
        public string Paid { get; set; } = string.Empty;
        public string Remarks { get; set; } = string.Empty;
        // Payment
        public bool IsCash { get; set; } = true;
        public string AccountDetail { get; set; } = string.Empty;

        // Checkboxes
        public bool WarrantyCardGiven { get; set; }
        public bool VoucherGivenToCustomer { get; set; }
        public bool VoucherIssuedByCompany { get; set; }
        // Models/InvoiceDraftData.cs  — add this property
        public DateTime? DueDate { get; set; }

        // Bike items (only filled rows)
        public List<DraftBikeItem> Items { get; set; } = new();
    }

    public class DraftBikeItem
    {
        public int? BikeId { get; set; }
        public string Model { get; set; } = string.Empty;
        public string Brand { get; set; } = string.Empty;
        public string MotorPower { get; set; } = string.Empty;
        public string Color { get; set; } = string.Empty;
        public string MotorNumber { get; set; } = string.Empty;
        public string ChassisNumber { get; set; } = string.Empty;
        public string Warranty { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int Quantity { get; set; } = 1;
    }
}