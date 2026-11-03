using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NewComputerStore.Data;
using NewComputerStore.Dtos;
using NewComputerStore.Models;

namespace NewComputerStore.Api
{
    [ApiController]
    [Route("api/[controller]")]
    public class ProductsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public ProductsController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpGet]
        [ProducesResponseType(typeof(PagedResultDto<ProductListItemDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetProducts(
            [FromQuery] int? categoryId = null,
            [FromQuery] string? q = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 20,
            [FromQuery] string? sort = null)
        {
            if (page < 1) page = 1;
            if (pageSize < 1) pageSize = 20;
            if (pageSize > 100) pageSize = 100;

            var query = _context.Products
                .AsNoTracking()
                .Include(p => p.Category)
                .Where(p => !p.IsHidden && !p.IsDeleted);

            if (categoryId.HasValue)
            {
                query = query.Where(p => p.CategoryId == categoryId.Value);
            }

            if (!string.IsNullOrWhiteSpace(q))
            {
                var keyword = q.Trim();
                query = query.Where(p =>
                    p.ProductName.Contains(keyword) ||
                    (p.Description != null && p.Description.Contains(keyword)));
            }

            var now = DateTime.Now;
            query = (sort?.Trim().ToLowerInvariant()) switch
            {
                "price_asc" => query.OrderBy(p =>
                    p.DiscountPercent > 0 &&
                    (!p.DiscountStartDate.HasValue || p.DiscountStartDate <= now) &&
                    (!p.DiscountEndDate.HasValue || p.DiscountEndDate >= now)
                        ? p.Price * (100m - p.DiscountPercent) / 100m
                        : p.Price),
                "price_desc" => query.OrderByDescending(p =>
                    p.DiscountPercent > 0 &&
                    (!p.DiscountStartDate.HasValue || p.DiscountStartDate <= now) &&
                    (!p.DiscountEndDate.HasValue || p.DiscountEndDate >= now)
                        ? p.Price * (100m - p.DiscountPercent) / 100m
                        : p.Price),
                "name_asc" => query.OrderBy(p => p.ProductName),
                "name_desc" => query.OrderByDescending(p => p.ProductName),
                _ => query.OrderByDescending(p => p.ProductId)
            };

            var totalItems = await query.CountAsync();
            var totalPages = totalItems == 0 ? 0 : (int)Math.Ceiling(totalItems / (double)pageSize);

            var products = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = products.Select(p =>
            {
                var (effectiveDiscount, finalPrice) = CalculatePricing(p, now);
                return new ProductListItemDto
                {
                    ProductId = p.ProductId,
                    ProductName = p.ProductName,
                    CategoryId = p.CategoryId,
                    CategoryName = p.Category?.CategoryName,
                    Price = p.Price,
                    DiscountPercent = effectiveDiscount,
                    FinalPrice = finalPrice,
                    Quantity = p.Quantity,
                    InStock = p.Quantity > 0,
                    ImageUrl = ToAbsoluteImageUrl(p.ImageUrl)
                };
            }).ToList();

            return Ok(new PagedResultDto<ProductListItemDto>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalPages
            });
        }

        [HttpGet("{id:int}")]
        [ProducesResponseType(typeof(ProductDetailDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetProductById(int id)
        {
            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductColors)
                .Include(p => p.ProductSpecs)
                    .ThenInclude(ps => ps.SpecKey)
                        .ThenInclude(sk => sk.Group)
                .FirstOrDefaultAsync(p => p.ProductId == id);

            if (product == null || product.IsDeleted || product.IsHidden)
            {
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Không tìm thấy sản phẩm",
                    Detail = $"Không tìm thấy sản phẩm với mã #{id} hoặc sản phẩm đã ngừng kinh doanh.",
                    Instance = Request.Path
                };
                problem.Extensions["code"] = "PRODUCT_NOT_FOUND";
                return NotFound(problem);
            }

            product.LastViewedDate = DateTime.Now;
            await _context.SaveChangesAsync();

            var now = DateTime.Now;
            var (effectiveDiscount, finalPrice) = CalculatePricing(product, now);

            var mainImageUrl = ToAbsoluteImageUrl(product.ImageUrl);
            var additionalImageUrls = string.IsNullOrWhiteSpace(product.AdditionalImages)
                ? new List<string>()
                : product.AdditionalImages
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Select(ToAbsoluteImageUrl)
                    .Where(url => !string.IsNullOrEmpty(url))
                    .Select(url => url!)
                    .ToList();

            var galleryImages = new List<string>();
            if (!string.IsNullOrEmpty(mainImageUrl))
            {
                galleryImages.Add(mainImageUrl);
            }
            galleryImages.AddRange(additionalImageUrls);

            var colors = (product.ProductColors ?? Array.Empty<ProductColor>())
                .OrderBy(c => c.Id)
                .Select(c => new ProductColorDto
                {
                    Id = c.Id,
                    ColorName = c.ColorName,
                    ColorCode = c.ColorCode,
                    Quantity = c.Quantity,
                    InStock = c.Quantity > 0,
                    ImageUrl = ToAbsoluteImageUrl(c.ImageUrl)
                })
                .ToList();

            var specGroups = (product.ProductSpecs ?? Array.Empty<ProductSpec>())
                .Where(ps => !string.IsNullOrWhiteSpace(ps.Value) && ps.SpecKey?.Group != null)
                .GroupBy(ps => ps.SpecKey.Group)
                .OrderBy(g => g.Key.DisplayOrder)
                .Select(g => new SpecGroupDto
                {
                    GroupId = g.Key.Id,
                    GroupName = g.Key.Name,
                    DisplayOrder = g.Key.DisplayOrder,
                    Specs = g.OrderBy(ps => ps.SpecKey.DisplayOrder)
                        .Select(ps => new ProductSpecItemDto
                        {
                            SpecKeyId = ps.SpecKeyId,
                            Name = ps.SpecKey.Name,
                            Value = ps.Value,
                            DisplayOrder = ps.SpecKey.DisplayOrder
                        })
                        .ToList()
                })
                .ToList();

            var dto = new ProductDetailDto
            {
                ProductId = product.ProductId,
                ProductName = product.ProductName,
                Description = product.Description,
                CategoryId = product.CategoryId,
                CategoryName = product.Category?.CategoryName,
                Price = product.Price,
                DiscountPercent = effectiveDiscount,
                DiscountStartDate = product.DiscountStartDate,
                DiscountEndDate = product.DiscountEndDate,
                FinalPrice = finalPrice,
                Quantity = product.Quantity,
                InStock = product.Quantity > 0,
                ImageUrl = mainImageUrl,
                AdditionalImages = additionalImageUrls,
                GalleryImages = galleryImages,
                Colors = colors,
                SpecGroups = specGroups
            };

            return Ok(dto);
        }

        [HttpGet("{id:int}/reviews")]
        [ProducesResponseType(typeof(ProductReviewsResponseDto), StatusCodes.Status200OK)]
        [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
        public async Task<IActionResult> GetProductReviews(int id)
        {
            var productExists = await _context.Products
                .AsNoTracking()
                .AnyAsync(p => p.ProductId == id && !p.IsDeleted && !p.IsHidden);

            if (!productExists)
            {
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Không tìm thấy sản phẩm",
                    Detail = $"Không tìm thấy sản phẩm với mã #{id} hoặc sản phẩm đã ngừng kinh doanh.",
                    Instance = Request.Path
                };
                problem.Extensions["code"] = "PRODUCT_NOT_FOUND";
                return NotFound(problem);
            }

            var reviews = await _context.Reviews
                .AsNoTracking()
                .Include(r => r.User)
                .Where(r => r.ProductId == id)
                .OrderByDescending(r => r.ReviewId)
                .Select(r => new ReviewDto
                {
                    ReviewId = r.ReviewId,
                    ProductId = r.ProductId,
                    UserId = r.UserId,
                    UserFullName = r.User != null ? r.User.FullName : "Khách hàng",
                    Rating = r.Rating,
                    Comment = r.Comment
                })
                .ToListAsync();

            double averageRating = reviews.Count > 0
                ? Math.Round(reviews.Average(r => r.Rating), 1)
                : 0.0;

            return Ok(new ProductReviewsResponseDto
            {
                ProductId = id,
                TotalReviews = reviews.Count,
                AverageRating = averageRating,
                Items = reviews
            });
        }

        private static (decimal EffectiveDiscountPercent, decimal FinalPrice) CalculatePricing(Product product, DateTime now)
        {
            bool isDiscountActive = product.DiscountPercent > 0
                && (!product.DiscountStartDate.HasValue || product.DiscountStartDate.Value <= now)
                && (!product.DiscountEndDate.HasValue || product.DiscountEndDate.Value >= now);

            decimal effectiveDiscount = isDiscountActive ? product.DiscountPercent : 0m;
            decimal calculatedPrice = product.Price - (product.Price * effectiveDiscount / 100m);
            decimal finalPrice = Math.Max(0m, Math.Round(calculatedPrice, 0, MidpointRounding.AwayFromZero));

            return (effectiveDiscount, finalPrice);
        }

        private string GetPublicBaseUrl()
        {
            var configured = _configuration["PublicBaseUrl"]?.Trim().TrimEnd('/');
            if (!string.IsNullOrEmpty(configured))
            {
                return configured;
            }

            return $"{Request.Scheme}://{Request.Host}";
        }

        private string? ToAbsoluteImageUrl(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            var trimmed = fileName.Trim();
            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                return trimmed;
            }

            return $"{GetPublicBaseUrl()}/images/{trimmed.TrimStart('/')}";
        }
    }
}
