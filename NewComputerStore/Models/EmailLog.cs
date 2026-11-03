using System;
using System.ComponentModel.DataAnnotations;

namespace NewComputerStore.Models
{
    public class EmailLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Receiver { get; set; }

        [Required]
        [StringLength(250)]
        public string Subject { get; set; }

        [Required]
        public string Body { get; set; }

        [Required]
        public DateTime SentTime { get; set; } = DateTime.Now;

        [Required]
        [StringLength(50)]
        public string Status { get; set; } // Success | Failed

        public string? ErrorMessage { get; set; }

        [Required]
        [StringLength(50)]
        public string EmailType { get; set; } // ConfirmEmail | WelcomeEmail | ResetPassword | OrderCreated | OrderConfirmed | OrderShipping | OrderDelivered | OrderCancelled | SecurityAlert
    }
}
