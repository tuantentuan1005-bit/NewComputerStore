using System.ComponentModel.DataAnnotations;

namespace NewComputerStore.ViewModels
{
    public class CheckoutViewModel
    {
        [Required(ErrorMessage = "Full Name is required")]
        [StringLength(100)]
        [Display(Name = "Full Name")]
        public string FullName { get; set; }

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress]
        public string Email { get; set; }

        [Required(ErrorMessage = "Phone number is required")]
        [Phone]
        [StringLength(20)]
        public string Phone { get; set; }

        [Required(ErrorMessage = "Address is required")]
        [StringLength(255)]
        public string Address { get; set; }

        public decimal TotalAmount { get; set; }

        public string PaymentMethod { get; set; } = "COD";
    }
}
