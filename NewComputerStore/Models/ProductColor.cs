using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewComputerStore.Models
{
    public class ProductColor
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public int ProductId { get; set; }

        [ForeignKey("ProductId")]
        [Microsoft.AspNetCore.Mvc.ModelBinding.Validation.ValidateNever]
        public virtual Product Product { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Color Name")]
        public string ColorName { get; set; } // e.g., "Red", "Blue"

        [Required]
        [StringLength(10)]
        [Display(Name = "Color Code (HEX)")]
        public string ColorCode { get; set; } // e.g., "#FF0000"

        [Required]
        [Range(0, int.MaxValue)]
        public int Quantity { get; set; } // Stock per color
        
        public string? ImageUrl { get; set; } // Image for this specific color

        [Timestamp]
        public byte[] RowVersion { get; set; }
    }
}
