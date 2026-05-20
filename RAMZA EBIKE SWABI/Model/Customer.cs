using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Ramza_EBike_Swabi.Models
{
    public class Customer
    {
        [Key]
        public int CustomerId { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string FatherName { get; set; } = string.Empty;

        [Required]
        public string Contact { get; set; } = string.Empty;

        public string CNIC { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;

        public ICollection<CustomerInvoice> Invoices { get; set; } = new List<CustomerInvoice>();
    }
}
