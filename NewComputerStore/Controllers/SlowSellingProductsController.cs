using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using NewComputerStore.Data;
using NewComputerStore.Models;
using NewComputerStore.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace NewComputerStore.Controllers
{
    [Authorize(Roles = "Admin")]
    public class SlowSellingProductsController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IMemoryCache _cache;
        private const string CacheKey = "SlowSellingProducts_CacheKey";

        public SlowSellingProductsController(AppDbContext context, IMemoryCache cache)
        {
            _context = context;
            _cache = cache;
        }

        private void ClearCache()
        {
            _cache.Remove(CacheKey);
        }

        private async Task<List<SlowSellingProductViewModel>> GetSlowSellingProductsAsync()
        {
            if (!_cache.TryGetValue(CacheKey, out List<SlowSellingProductViewModel> products))
            {
                var today = DateTime.Now;
                var thirtyDaysAgo = today.AddDays(-30);

                var data = await _context.Products
                    .Where(p => !p.IsDeleted)
                    .Select(p => new {
                        ProductId = p.ProductId,
                        ProductName = p.ProductName,
                        ImageUrl = p.ImageUrl,
                        CategoryName = p.Category.CategoryName,
                        CategoryId = p.CategoryId,
                        Price = p.Price,
                        DiscountPercent = p.DiscountPercent,
                        StockQuantity = p.Quantity,
                        IsHidden = p.IsHidden,
                        IsDeleted = p.IsDeleted,
                        LastViewedDate = p.LastViewedDate,
                        TotalSold = p.OrderDetails.Where(od => od.Order.Status != "Cancelled").Sum(od => (int?)od.Quantity) ?? 0,
                        TotalSold30Days = p.OrderDetails.Where(od => od.Order.Status != "Cancelled" && od.Order.OrderDate >= thirtyDaysAgo).Sum(od => (int?)od.Quantity) ?? 0,
                        LastSoldDate = p.OrderDetails.Where(od => od.Order.Status != "Cancelled").Max(od => (DateTime?)od.Order.OrderDate)
                    })
                    .AsNoTracking()
                    .ToListAsync();

                products = new List<SlowSellingProductViewModel>();

                foreach (var item in data)
                {
                    int daysSinceLastSale = item.LastSoldDate.HasValue 
                        ? (int)(today - item.LastSoldDate.Value).TotalDays 
                        : 365;

                    // ViewCount mock: TotalSold * random factor (deterministic by ProductId) + baseline
                    int factor = 5 + (item.ProductId % 11);
                    int baseline = 10 + (item.ProductId % 37);
                    int viewCount = item.TotalSold * factor + baseline;

                    double conversionRate = viewCount > 0 ? (item.TotalSold * 100.0) / viewCount : 0.0;
                    string conversionRateText = conversionRate < 1.0 ? "Rất kém" : (conversionRate <= 3.0 ? "Trung bình" : "Tốt");

                    double rawHeatScore = (item.TotalSold * 3) + (viewCount / 10.0) - item.StockQuantity - daysSinceLastSale;
                    double heatScore = Math.Max(-500.0, rawHeatScore);

                    // Status classification
                    string status = "Ổn định";
                    if (item.StockQuantity > 50 && daysSinceLastSale > 30)
                    {
                        status = "Tồn kho nguy hiểm";
                    }
                    else if (daysSinceLastSale > 30)
                    {
                        status = "Dead stock";
                    }
                    else if (item.TotalSold30Days < 5 && item.StockQuantity > 20)
                    {
                        status = "Ế nặng";
                    }
                    else if (item.TotalSold30Days < 5 || item.StockQuantity > 10 || daysSinceLastSale > 14 || item.TotalSold == 0)
                    {
                        status = "Bán chậm";
                    }

                    // Auto Recommendation Engine
                    string suggestedPromotion = "Combo khuyến mãi";
                    string suggestedReason = "Bán chậm";

                    if (viewCount > 50 && item.TotalSold <= 2)
                    {
                        suggestedPromotion = "Giảm giá nhẹ 5-10%";
                        suggestedReason = "Nhiều lượt xem nhưng ít mua";
                    }
                    else if (item.StockQuantity > 20)
                    {
                        suggestedPromotion = "Flash Sale -20%";
                        suggestedReason = "Tồn kho quá cao";
                    }
                    else if (item.TotalSold == 0)
                    {
                        suggestedPromotion = "Đẩy quảng cáo";
                        suggestedReason = "Không có lượt mua";
                    }
                    else if (daysSinceLastSale > 30)
                    {
                        suggestedPromotion = "Xả kho";
                        suggestedReason = "Tồn kho quá lâu";
                    }

                    products.Add(new SlowSellingProductViewModel
                    {
                        ProductId = item.ProductId,
                        ProductName = item.ProductName,
                        ImageUrl = item.ImageUrl,
                        CategoryName = item.CategoryName,
                        Price = item.Price,
                        DiscountPercent = item.DiscountPercent,
                        TotalSold = item.TotalSold,
                        StockQuantity = item.StockQuantity,
                        LastSoldDate = item.LastSoldDate,
                        DaysSinceLastSale = daysSinceLastSale,
                        LastViewedDate = item.LastViewedDate,
                        InventoryValue = item.Price * item.StockQuantity,
                        ViewCount = viewCount,
                        Status = status,
                        SuggestedPromotion = suggestedPromotion,
                        SuggestedReason = suggestedReason,
                        HeatScore = heatScore,
                        ConversionRate = conversionRate,
                        ConversionRateText = conversionRateText,
                        IsHidden = item.IsHidden,
                        IsDeleted = item.IsDeleted
                    });
                }

                var cacheOptions = new MemoryCacheEntryOptions()
                    .SetAbsoluteExpiration(TimeSpan.FromMinutes(2));

                _cache.Set(CacheKey, products, cacheOptions);
            }

            return products;
        }

        [HttpGet]
        public async Task<IActionResult> Index(
            int? categoryId,
            string status,
            string search,
            int? minStock,
            int? maxStock,
            int? minSold,
            int? maxSold,
            string sortBy = "heatscore_asc",
            int page = 1,
            int pageSize = 10)
        {
            var allProducts = await GetSlowSellingProductsAsync();

            // Total statistics (always computed before filters)
            ViewBag.TotalSlowSelling = allProducts.Count(p => p.Status == "Bán chậm" || p.Status == "Ế nặng" || p.Status == "Dead stock" || p.Status == "Tồn kho nguy hiểm");
            ViewBag.TotalHighStock = allProducts.Count(p => p.StockQuantity > 10);
            ViewBag.TotalNeverSold = allProducts.Count(p => p.TotalSold == 0);
            ViewBag.TotalInventoryValue = allProducts.Sum(p => p.InventoryValue);

            // Chart data preparation
            // 1. Status pie chart data
            var statusCounts = new Dictionary<string, int>
            {
                { "Ế nặng", allProducts.Count(p => p.Status == "Ế nặng") },
                { "Bán chậm", allProducts.Count(p => p.Status == "Bán chậm") },
                { "Tồn kho nguy hiểm", allProducts.Count(p => p.Status == "Tồn kho nguy hiểm") },
                { "Dead stock", allProducts.Count(p => p.Status == "Dead stock") },
                { "Ổn định", allProducts.Count(p => p.Status == "Ổn định") }
            };
            ViewBag.StatusCounts = statusCounts;

            // 2. Inventory distribution by category
            var categoryInventory = allProducts
                .GroupBy(p => p.CategoryName)
                .Select(g => new { Category = g.Key, Value = g.Sum(p => p.InventoryValue) })
                .ToDictionary(x => x.Category, x => x.Value);
            ViewBag.CategoryInventory = categoryInventory;

            // 3. Top 10 slowest selling by HeatScore
            var top10Slowest = allProducts
                .OrderBy(p => p.HeatScore)
                .Take(10)
                .ToList();
            ViewBag.Top10Slowest = top10Slowest;

            // Apply Filters
            var filteredProducts = allProducts.AsEnumerable();

            if (categoryId.HasValue)
            {
                var catName = await _context.Categories.Where(c => c.CategoryId == categoryId.Value).Select(c => c.CategoryName).FirstOrDefaultAsync();
                if (catName != null)
                {
                    filteredProducts = filteredProducts.Where(p => p.CategoryName == catName);
                }
            }

            if (!string.IsNullOrEmpty(status))
            {
                filteredProducts = filteredProducts.Where(p => p.Status == status);
            }

            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower().Trim();
                filteredProducts = filteredProducts.Where(p => p.ProductName.ToLower().Contains(search) || p.ProductId.ToString() == search);
            }

            if (minStock.HasValue) filteredProducts = filteredProducts.Where(p => p.StockQuantity >= minStock.Value);
            if (maxStock.HasValue) filteredProducts = filteredProducts.Where(p => p.StockQuantity <= maxStock.Value);
            if (minSold.HasValue) filteredProducts = filteredProducts.Where(p => p.TotalSold >= minSold.Value);
            if (maxSold.HasValue) filteredProducts = filteredProducts.Where(p => p.TotalSold <= maxSold.Value);

            // Sorting
            filteredProducts = sortBy switch
            {
                "heatscore_asc" => filteredProducts.OrderBy(p => p.HeatScore),
                "heatscore_desc" => filteredProducts.OrderByDescending(p => p.HeatScore),
                "stock_desc" => filteredProducts.OrderByDescending(p => p.StockQuantity),
                "stock_asc" => filteredProducts.OrderBy(p => p.StockQuantity),
                "sold_desc" => filteredProducts.OrderByDescending(p => p.TotalSold),
                "sold_asc" => filteredProducts.OrderBy(p => p.TotalSold),
                "name_asc" => filteredProducts.OrderBy(p => p.ProductName),
                _ => filteredProducts.OrderBy(p => p.HeatScore)
            };

            // Pagination
            int totalItems = filteredProducts.Count();
            int totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            page = Math.Max(1, Math.Min(totalPages, page));

            var pagedProducts = filteredProducts.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            ViewBag.Categories = await _context.Categories.AsNoTracking().ToListAsync();
            ViewBag.CategoryId = categoryId;
            ViewBag.Status = status;
            ViewBag.Search = search;
            ViewBag.MinStock = minStock;
            ViewBag.MaxStock = maxStock;
            ViewBag.MinSold = minSold;
            ViewBag.MaxSold = maxSold;
            ViewBag.SortBy = sortBy;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalItems = totalItems;

            return View(pagedProducts);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleHide(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return Json(new { success = false, message = "Không tìm thấy sản phẩm!" });
            }

            var oldValue = product.IsHidden;
            product.IsHidden = !product.IsHidden;
            
            var email = User.FindFirstValue(ClaimTypes.Email) ?? "admin@store.com";
            _context.ActivityLogs.Add(new ActivityLog
            {
                Action = $"Admin: {email} | ProductId: {product.ProductId} | ActionType: ToggleHide | Timestamp: {DateTime.Now} | OldValue: {oldValue} | NewValue: {product.IsHidden}",
                UserEmail = email,
                Time = DateTime.Now
            });

            await _context.SaveChangesAsync();
            ClearCache();

            return Json(new { success = true, isHidden = product.IsHidden, message = $"Đã {(product.IsHidden ? "ẩn khỏi trang chủ" : "hiển thị lại trang chủ")} sản phẩm {product.ProductName}!" });
        }

        [HttpPost]
        public async Task<IActionResult> Discontinue(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return Json(new { success = false, message = "Không tìm thấy sản phẩm!" });
            }

            // SOFT DELETE RULE
            // 1. Check if product has pending orders
            bool hasPendingOrders = await _context.OrderDetails
                .AnyAsync(od => od.ProductId == id && (od.Order.Status == "Pending" || od.Order.Status == "Processing"));
            if (hasPendingOrders)
            {
                return Json(new { success = false, message = "Không thể ngừng kinh doanh sản phẩm này vì đang có đơn hàng chờ xử lý (Pending hoặc Processing)!" });
            }

            product.IsDeleted = true;

            var email = User.FindFirstValue(ClaimTypes.Email) ?? "admin@store.com";
            _context.ActivityLogs.Add(new ActivityLog
            {
                Action = $"Admin: {email} | ProductId: {product.ProductId} | ActionType: SoftDelete | Timestamp: {DateTime.Now} | OldValue: False | NewValue: True",
                UserEmail = email,
                Time = DateTime.Now
            });

            await _context.SaveChangesAsync();
            ClearCache();

            return Json(new { success = true, message = $"Đã ngừng kinh doanh (Xóa mềm) sản phẩm {product.ProductName} thành công!" });
        }

        [HttpPost]
        public async Task<IActionResult> ApplyDiscount(int id, decimal percentage)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null)
            {
                return Json(new { success = false, message = "Không tìm thấy sản phẩm!" });
            }

            if (product.IsDeleted)
            {
                return Json(new { success = false, message = "Không thể áp dụng giảm giá cho sản phẩm đã ngừng kinh doanh!" });
            }
            if (product.Quantity <= 0)
            {
                return Json(new { success = false, message = "Không thể áp dụng giảm giá cho sản phẩm đã hết hàng!" });
            }

            var oldDiscount = product.DiscountPercent;
            product.DiscountPercent = percentage;

            var email = User.FindFirstValue(ClaimTypes.Email) ?? "admin@store.com";
            string actionType = percentage > 0 ? "APPLY_DISCOUNT" : "REMOVE_DISCOUNT";
            
            _context.ActivityLogs.Add(new ActivityLog
            {
                Action = $"Admin: {email} | ProductId: {product.ProductId} | ActionType: {actionType} | Timestamp: {DateTime.Now} | OldDiscount: {oldDiscount}% | NewDiscount: {percentage}%",
                UserEmail = email,
                Time = DateTime.Now
            });

            await _context.SaveChangesAsync();
            ClearCache();

            return Json(new { success = true, message = $"Đã {(percentage > 0 ? $"áp dụng giảm giá {percentage}%" : "xóa giảm giá")} cho sản phẩm {product.ProductName} thành công!" });
        }

        [HttpPost]
        public async Task<IActionResult> BulkAction(int[] productIds, string actionName, decimal? discountPercent)
        {
            if (productIds == null || productIds.Length == 0)
            {
                return Json(new { success = false, message = "Chưa chọn sản phẩm nào!" });
            }

            var email = User.FindFirstValue(ClaimTypes.Email) ?? "admin@store.com";
            int successCount = 0;
            int skippedCount = 0;
            var skippedNames = new List<string>();

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                foreach (var id in productIds)
                {
                    var product = await _context.Products.FindAsync(id);
                    if (product == null) continue;

                    if (actionName == "hide")
                    {
                        var oldVal = product.IsHidden;
                        product.IsHidden = true;
                        _context.ActivityLogs.Add(new ActivityLog
                        {
                            Action = $"Admin: {email} | ProductId: {product.ProductId} | ActionType: BulkHide | Timestamp: {DateTime.Now} | OldValue: {oldVal} | NewValue: True",
                            UserEmail = email
                        });
                        successCount++;
                    }
                    else if (actionName == "show")
                    {
                        var oldVal = product.IsHidden;
                        product.IsHidden = false;
                        _context.ActivityLogs.Add(new ActivityLog
                        {
                            Action = $"Admin: {email} | ProductId: {product.ProductId} | ActionType: BulkShow | Timestamp: {DateTime.Now} | OldValue: {oldVal} | NewValue: False",
                            UserEmail = email
                        });
                        successCount++;
                    }
                    else if (actionName == "delete")
                    {
                        // Soft delete rule check
                        bool hasPending = await _context.OrderDetails.AnyAsync(od => od.ProductId == id && (od.Order.Status == "Pending" || od.Order.Status == "Processing"));
                        if (hasPending)
                        {
                            skippedCount++;
                            skippedNames.Add(product.ProductName);
                            continue;
                        }

                        product.IsDeleted = true;
                        _context.ActivityLogs.Add(new ActivityLog
                        {
                            Action = $"Admin: {email} | ProductId: {product.ProductId} | ActionType: BulkDelete | Timestamp: {DateTime.Now} | OldValue: False | NewValue: True",
                            UserEmail = email
                        });
                        successCount++;
                    }
                    else if (actionName == "flash_sale")
                    {
                        decimal pct = discountPercent ?? 20;
                        if (product.IsDeleted || product.Quantity <= 0)
                        {
                            skippedCount++;
                            skippedNames.Add(product.ProductName + " (Đã xóa hoặc hết hàng)");
                            continue;
                        }

                        var oldDiscount = product.DiscountPercent;
                        product.DiscountPercent = pct;

                        _context.ActivityLogs.Add(new ActivityLog
                        {
                            Action = $"Admin: {email} | ProductId: {product.ProductId} | ActionType: FLASH_SALE | Timestamp: {DateTime.Now} | OldDiscount: {oldDiscount}% | NewDiscount: {pct}%",
                            UserEmail = email,
                            Time = DateTime.Now
                        });
                        successCount++;
                    }
                    else if (actionName == "remove_discount")
                    {
                        var oldDiscount = product.DiscountPercent;
                        product.DiscountPercent = 0;

                        _context.ActivityLogs.Add(new ActivityLog
                        {
                            Action = $"Admin: {email} | ProductId: {product.ProductId} | ActionType: REMOVE_DISCOUNT | Timestamp: {DateTime.Now} | OldDiscount: {oldDiscount}% | NewDiscount: 0%",
                            UserEmail = email,
                            Time = DateTime.Now
                        });
                        successCount++;
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                ClearCache();

                string msg = $"Thao tác thành công trên {successCount} sản phẩm.";
                if (skippedCount > 0)
                {
                    msg += $" Đã bỏ qua {skippedCount} sản phẩm có đơn hàng Pending/Processing: {string.Join(", ", skippedNames)}";
                }

                return Json(new { success = true, message = msg });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = "Thao tác thất bại và đã hoàn tác (Rollback): " + ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(string type, int? categoryId, string status, string search)
        {
            var products = await GetSlowSellingProductsAsync();

            // Apply filters similar to Index
            var filtered = products.AsEnumerable();
            if (categoryId.HasValue)
            {
                var catName = await _context.Categories.Where(c => c.CategoryId == categoryId.Value).Select(c => c.CategoryName).FirstOrDefaultAsync();
                if (catName != null) filtered = filtered.Where(p => p.CategoryName == catName);
            }
            if (!string.IsNullOrEmpty(status)) filtered = filtered.Where(p => p.Status == status);
            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower().Trim();
                filtered = filtered.Where(p => p.ProductName.ToLower().Contains(search) || p.ProductId.ToString() == search);
            }

            // Apply specific type filter (Top bán chậm, Dead stock, Tồn kho)
            filtered = type switch
            {
                "slow" => filtered.Where(p => p.Status == "Ế nặng" || p.Status == "Bán chậm"),
                "dead" => filtered.Where(p => p.Status == "Dead stock" || p.Status == "Tồn kho nguy hiểm"),
                "stock" => filtered.OrderByDescending(p => p.StockQuantity),
                _ => filtered
            };

            var dataList = filtered.ToList();

            var adminEmail = User.FindFirstValue(ClaimTypes.Email) ?? "admin@store.com";
            var adminName = await _context.Users.Where(u => u.Email == adminEmail).Select(u => u.FullName).FirstOrDefaultAsync() ?? "Administrator";
            var dateStr = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            
            // Build an HTML table matching Excel requirements
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'/><style>table { border-collapse: collapse; } th, td { border: 1px solid #ddd; padding: 8px; text-align: left; } th { background-color: #f2f2f2; }</style></head><body>");
            sb.AppendLine($"<h2>BÁO CÁO PHÂN TÍCH SẢN PHẨM BÁN CHẬM</h2>");
            sb.AppendLine($"<p><b>Cửa hàng:</b> New Computer Store</p>");
            sb.AppendLine($"<p><b>Phân loại xuất:</b> {(type == "slow" ? "Top bán chậm" : (type == "dead" ? "Dead stock" : (type == "stock" ? "Tồn kho cao" : "Tất cả")))}</p>");
            sb.AppendLine($"<p><b>Ngày xuất báo cáo:</b> {dateStr}</p>");
            sb.AppendLine($"<p><b>Người xuất báo cáo:</b> {adminName} ({adminEmail})</p>");
            sb.AppendLine($"<p><b>Tổng số sản phẩm:</b> {dataList.Count}</p>");
            sb.AppendLine($"<p><b>Tổng giá trị tồn kho:</b> {dataList.Sum(p => p.InventoryValue):N0} VNĐ</p><br/>");

            sb.AppendLine("<table>");
            sb.AppendLine("<thead><tr><th>Mã SP</th><th>Tên sản phẩm</th><th>Danh mục</th><th>Đã bán</th><th>Tồn kho</th><th>Lượt xem</th><th>Giá gốc</th><th>Giảm %</th><th>Giá sau giảm</th><th>Giá trị tồn</th><th>Ngày bán cuối</th><th>Số ngày chưa bán</th><th>Trạng thái</th><th>HeatScore</th><th>Đề xuất</th></tr></thead>");
            sb.AppendLine("<tbody>");
            foreach (var p in dataList)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{p.ProductId}</td>");
                sb.AppendLine($"<td>{p.ProductName}</td>");
                sb.AppendLine($"<td>{p.CategoryName}</td>");
                sb.AppendLine($"<td>{p.TotalSold}</td>");
                sb.AppendLine($"<td>{p.StockQuantity}</td>");
                sb.AppendLine($"<td>{p.ViewCount}</td>");
                sb.AppendLine($"<td>{p.Price:N0} VNĐ</td>");
                sb.AppendLine($"<td>{p.DiscountPercent:0}%</td>");
                sb.AppendLine($"<td>{p.FinalPrice:N0} VNĐ</td>");
                sb.AppendLine($"<td>{p.InventoryValue:N0} VNĐ</td>");
                sb.AppendLine($"<td>{(p.LastSoldDate.HasValue ? p.LastSoldDate.Value.ToString("dd/MM/yyyy") : "Chưa từng bán")}</td>");
                sb.AppendLine($"<td>{p.DaysSinceLastSale}</td>");
                sb.AppendLine($"<td>{p.Status}</td>");
                sb.AppendLine($"<td>{p.HeatScore:0.##}</td>");
                sb.AppendLine($"<td>{p.SuggestedPromotion} ({p.SuggestedReason})</td>");
                sb.AppendLine("</tr>");
            }
            sb.AppendLine("</tbody></table></body></html>");

            var fileName = $"SlowSellingProducts_{DateTime.Now:yyyyMMdd_HHmm}.xls";
            return File(Encoding.UTF8.GetBytes(sb.ToString()), "application/vnd.ms-excel", fileName);
        }

        [HttpGet]
        public async Task<IActionResult> ExportPdfView(string type, int? categoryId, string status, string search)
        {
            var products = await GetSlowSellingProductsAsync();

            var filtered = products.AsEnumerable();
            if (categoryId.HasValue)
            {
                var catName = await _context.Categories.Where(c => c.CategoryId == categoryId.Value).Select(c => c.CategoryName).FirstOrDefaultAsync();
                if (catName != null) filtered = filtered.Where(p => p.CategoryName == catName);
            }
            if (!string.IsNullOrEmpty(status)) filtered = filtered.Where(p => p.Status == status);
            if (!string.IsNullOrEmpty(search))
            {
                search = search.ToLower().Trim();
                filtered = filtered.Where(p => p.ProductName.ToLower().Contains(search) || p.ProductId.ToString() == search);
            }

            filtered = type switch
            {
                "slow" => filtered.Where(p => p.Status == "Ế nặng" || p.Status == "Bán chậm"),
                "dead" => filtered.Where(p => p.Status == "Dead stock" || p.Status == "Tồn kho nguy hiểm"),
                "stock" => filtered.OrderByDescending(p => p.StockQuantity),
                _ => filtered
            };

            var dataList = filtered.ToList();

            var adminEmail = User.FindFirstValue(ClaimTypes.Email) ?? "admin@store.com";
            ViewBag.AdminName = await _context.Users.Where(u => u.Email == adminEmail).Select(u => u.FullName).FirstOrDefaultAsync() ?? "Administrator";
            ViewBag.AdminEmail = adminEmail;
            ViewBag.ExportDate = DateTime.Now.ToString("dd/MM/yyyy HH:mm");
            ViewBag.ReportType = type switch
            {
                "slow" => "Top bán chậm",
                "dead" => "Dead stock",
                "stock" => "Tồn kho cao",
                _ => "Tất cả sản phẩm phân tích"
            };

            return View("PdfExport", dataList);
        }
    }
}
