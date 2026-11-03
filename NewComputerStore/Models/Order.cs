using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewComputerStore.Models
{
    public class Order
    {
        [Key]
        public int OrderId { get; set; }

        [Required]
        public int UserId { get; set; }

        [ForeignKey("UserId")]
        public User User { get; set; }

        [Required]
        [Display(Name = "Order Date")]
        public DateTime OrderDate { get; set; }

        [Required]
        [Column(TypeName = "decimal(18,2)")]
        [Display(Name = "Total Price")]
        public decimal TotalPrice { get; set; }

        [Required]
        [StringLength(50)]
        public string Status { get; set; } // Pending, Processing, Shipped, Delivered, Cancelled

        [Required]
        [StringLength(100)]
        public string FullName { get; set; }

        [Required]
        [StringLength(20)]
        public string Phone { get; set; }

        [Required]
        [StringLength(255)]
        public string Address { get; set; }

        [StringLength(50)]
        public string PaymentMethod { get; set; } = "COD"; // COD | QR

        [StringLength(50)]
        public string PaymentStatus { get; set; } = "Pending"; // Pending | Paid

        [StringLength(500)]
        public string? Note { get; set; }

        public ICollection<OrderDetail> OrderDetails { get; set; }

        public Order()
        {
            OrderDetails = new HashSet<OrderDetail>();
            OrderDate = DateTime.Now;
            Status = "Pending";
            PaymentMethod = "COD";
            PaymentStatus = "Pending";
            FullName = "";
            Phone = "";
            Address = "";
        }
    }
}
