using System;
using System.Collections.Generic;

namespace NewComputerStore.Dtos
{
    public class CategoryDto
    {
        public int CategoryId { get; set; }
        public string CategoryName { get; set; } = string.Empty;
    }

    public class PagedResultDto<T>
    {
        public List<T> Items { get; set; } = new List<T>();
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
    }

    public class ProductListItemDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public int CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public decimal Price { get; set; }
        public decimal DiscountPercent { get; set; }
        public decimal FinalPrice { get; set; }
        public int Quantity { get; set; }
        public bool InStock { get; set; }
        public string? ImageUrl { get; set; }
    }

    public class ProductDetailDto
    {
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int CategoryId { get; set; }
        public string? CategoryName { get; set; }
        public decimal Price { get; set; }
        public decimal DiscountPercent { get; set; }
        public DateTime? DiscountStartDate { get; set; }
        public DateTime? DiscountEndDate { get; set; }
        public decimal FinalPrice { get; set; }
        public int Quantity { get; set; }
        public bool InStock { get; set; }
        public string? ImageUrl { get; set; }
        public List<string> AdditionalImages { get; set; } = new List<string>();
        public List<string> GalleryImages { get; set; } = new List<string>();
        public List<ProductColorDto> Colors { get; set; } = new List<ProductColorDto>();
        public List<SpecGroupDto> SpecGroups { get; set; } = new List<SpecGroupDto>();
    }

    public class ProductColorDto
    {
        public int Id { get; set; }
        public string ColorName { get; set; } = string.Empty;
        public string ColorCode { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public bool InStock { get; set; }
        public string? ImageUrl { get; set; }
    }

    public class SpecGroupDto
    {
        public int GroupId { get; set; }
        public string GroupName { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
        public List<ProductSpecItemDto> Specs { get; set; } = new List<ProductSpecItemDto>();
    }

    public class ProductSpecItemDto
    {
        public int SpecKeyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public int DisplayOrder { get; set; }
    }
}
