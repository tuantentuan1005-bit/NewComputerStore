using Microsoft.AspNetCore.Http;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace NewComputerStore.ViewModels
{
    public class CustomSpecViewModel
    {
        public string? Key { get; set; }
        public string? Value { get; set; }
    }

    public class ProductColorViewModel
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ColorName { get; set; }
        public string ColorCode { get; set; }
        public int Quantity { get; set; }
        public string? ExistingImage { get; set; }
        public IFormFile? ColorImage { get; set; }
    }

    public class ProductViewModel
    {
        public int ProductId { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "Product Name")]
        public string ProductName { get; set; }

        [Required]
        [DisplayFormat(DataFormatString = "{0:0.##}", ApplyFormatInEditMode = true)]
        public decimal Price { get; set; }

        [Required]
        public int Quantity { get; set; }

        public string? Description { get; set; }

        // Dynamic Specs mapping: Key = SpecKeyId, Value = inputted value
        public Dictionary<int, string> Specs { get; set; } = new();

        // Custom Specs added by user manually
        public List<CustomSpecViewModel> CustomSpecs { get; set; } = new();

        [Display(Name = "Current Image")]
        public string? ExistingImage { get; set; }

        [Display(Name = "Upload Image")]
        public IFormFile? ProductImage { get; set; }

        /// <summary>Upload thêm nhiều ảnh cho gallery.</summary>
        [Display(Name = "Additional Images")]
        public List<IFormFile>? AdditionalImageUploads { get; set; }

        /// <summary>Danh sách tên file ảnh phụ đã có (dùng khi Edit).</summary>
        public List<string> ExistingAdditionalImages { get; set; } = new();

        [Required]
        [Display(Name = "Category")]
        public int CategoryId { get; set; }

        public List<ProductColorViewModel> ProductColors { get; set; } = new();
    }
}
