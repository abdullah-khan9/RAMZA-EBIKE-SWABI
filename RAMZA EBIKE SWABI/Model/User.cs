using System;
using System.ComponentModel.DataAnnotations;

namespace Ramza_EBike_Swabi.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Username { get; set; } = null!;

        [Required]
        public string PasswordHash { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string FullName { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string Role { get; set; } = "SalesPerson";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
