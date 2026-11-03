using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NewComputerStore.Models
{
    public class Product
    {
        [Key]
        public int ProductId { get; set; }

        [Required(ErrorMessage = NewComputerStore.Helpers.LocalizationHelper.Required)]
        [StringLength(200)]
        [Display(Name = "Product Name")]
        public string ProductName { get; set; }

        [Required(ErrorMessage = NewComputerStore.Helpers.LocalizationHelper.Required)]
        [Column(TypeName = "decimal(18,2)")]
        [Range(0, double.MaxValue)]
        public decimal Price { get; set; }

        [Required(ErrorMessage = NewComputerStore.Helpers.LocalizationHelper.Required)]
        [Range(0, int.MaxValue)]
        public int Quantity { get; set; }

        public string? Description { get; set; }

        [Display(Name = "Image URL")]
        public string? ImageUrl { get; set; }

        /// <summary>
        /// Comma-separated list of additional image filenames for the gallery/carousel.
        /// </summary>
        public string? AdditionalImages { get; set; }

        [Required]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }

        [ForeignKey("CategoryId")]
        public Category Category { get; set; }

        public bool IsHidden { get; set; } = false;
        public bool IsDeleted { get; set; } = false;
        public DateTime? LastViewedDate { get; set; }

        [Required]
        [Column(TypeName = "decimal(5,2)")]
        [Range(0.00, 90.00, ErrorMessage = "Tỷ lệ giảm giá phải từ 0% đến 90%.")]
        public decimal DiscountPercent { get; set; } = 0;

        public DateTime? DiscountStartDate { get; set; }
        public DateTime? DiscountEndDate { get; set; }
        public int? FlashSaleId { get; set; }

        [StringLength(50)]
        public string? VoucherCode { get; set; }

        [Timestamp]
        public byte[] RowVersion { get; set; }

        [NotMapped]
        public decimal FinalPrice
        {
            get
            {
                var calculatedPrice = Price - (Price * DiscountPercent / 100);
                return Math.Max(0, Math.Round(calculatedPrice, 0, MidpointRounding.AwayFromZero));
            }
        }

        public ICollection<OrderDetail> OrderDetails { get; set; }
        public virtual ICollection<ProductColor> ProductColors { get; set; }
        public ICollection<Review> Reviews { get; set; }
        public virtual ICollection<ProductSpec> ProductSpecs { get; set; }

        public Product()
        {
            OrderDetails = new HashSet<OrderDetail>();
            ProductColors = new HashSet<ProductColor>();
            Reviews = new HashSet<Review>();
            ProductSpecs = new HashSet<ProductSpec>();
        }
    }
}
