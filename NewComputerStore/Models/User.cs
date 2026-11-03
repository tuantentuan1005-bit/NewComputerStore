using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace NewComputerStore.Models
{
    public class User
    {
        [Key]
        public int UserId { get; set; }

        [Required]
        [StringLength(100)]
        public string FullName { get; set; }

        [Required]
        [EmailAddress]
        public string Email { get; set; }

        public string? Password { get; set; }

        [Required]
        public string Role { get; set; } // Admin, Customer, Staff

        public bool IsActive { get; set; } = true;

        public bool IsEmailVerified { get; set; } = false;

        public string? VerificationCode { get; set; } // Stored as SHA256 hash

        public DateTime? VerificationExpiry { get; set; }

        public int VerificationFailedAttempts { get; set; } = 0;

        public DateTime? LastOtpSentAt { get; set; }

        public bool IsDeleted { get; set; } = false;

        // Navigation Properties
        public ICollection<Order> Orders { get; set; }
        public ICollection<Review> Reviews { get; set; }

        public User()
        {
            Orders = new HashSet<Order>();
            Reviews = new HashSet<Review>();
        }
    }
}
