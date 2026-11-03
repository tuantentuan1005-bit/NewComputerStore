using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using NewComputerStore.Data;
using NewComputerStore.Models;
using NewComputerStore.ViewModels;
using System;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace NewComputerStore.Controllers
{
    public class ProductController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _webHostEnvironment;

        public ProductController(AppDbContext context, IWebHostEnvironment webHostEnvironment)
        {
            _context = context;
            _webHostEnvironment = webHostEnvironment;
        }

        // --- CUSTOMER FEATURES ---

        // GET: Product/Index
        public async Task<IActionResult> Index(string searchString, int? categoryId, int? pageNumber)
        {
            ViewData["CurrentFilter"] = searchString;
            ViewData["CurrentCategory"] = categoryId;

            var products = from p in _context.Products.Include(p => p.Category).Where(p => !p.IsHidden && !p.IsDeleted)
                           select p;

            if (!String.IsNullOrEmpty(searchString))
            {
                products = products.Where(s => s.ProductName.Contains(searchString)
                                       || s.Description.Contains(searchString));
            }

            if (categoryId.HasValue)
            {
                products = products.Where(p => p.CategoryId == categoryId);
            }

            ViewBag.Categories = await _context.Categories.ToListAsync();

            // Simple pagination
            int pageSize = 8;
            int pageIndex = pageNumber ?? 1;

            var count = await products.CountAsync();
            var items = await products.Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync();

            ViewBag.CurrentPage = pageIndex;
            ViewBag.TotalPages = (int)Math.Ceiling(count / (double)pageSize);

            return View(items);
        }

        public async Task<IActionResult> Details(int? id)
        {
            if (id == null || _context.Products == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.Category)
                .Include(p => p.ProductColors)
                .Include(p => p.Reviews)
                .ThenInclude(r => r.User)
                .Include(p => p.ProductSpecs)
                .ThenInclude(ps => ps.SpecKey)
                .ThenInclude(sk => sk.Group)
                .FirstOrDefaultAsync(m => m.ProductId == id);
            
            if (product == null || product.IsDeleted)
            {
                return NotFound();
            }

            // Ghi nhận LastViewedDate
            product.LastViewedDate = DateTime.Now;
            _context.Update(product);
            await _context.SaveChangesAsync();

            return View(product);
        }

        // --- ADMIN FEATURES ---

        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.WarehouseManager)]
        public async Task<IActionResult> AdminIndex()
        {
            var applicationDbContext = _context.Products.Include(p => p.Category).Where(p => !p.IsDeleted);
            return View(await applicationDbContext.ToListAsync());
        }

        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.WarehouseManager)]
        public IActionResult Create()
        {
            ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "CategoryName");
            return View(new ProductViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.WarehouseManager)]
        public async Task<IActionResult> Create(ProductViewModel model)
        {
            ModelState.Remove("ExistingImage");
            ModelState.Remove("ProductImage");

            // Validation: Must have at least one color
            if (model.ProductColors == null || !model.ProductColors.Any())
            {
                ModelState.AddModelError("ProductColors", "Please add at least one color variant.");
            }

            if (ModelState.IsValid)
            {
                var product = new Product
                {
                    ProductName = model.ProductName,
                    Price = model.Price,
                    Quantity = model.Quantity,
                    Description = model.Description,
                    CategoryId = model.CategoryId,
                };

                if (model.Specs != null && model.Specs.Any())
                {
                    foreach (var spec in model.Specs)
                    {
                        if (!string.IsNullOrWhiteSpace(spec.Value))
                        {
                            product.ProductSpecs.Add(new ProductSpec
                            {
                                SpecKeyId = spec.Key,
                                Value = spec.Value
                            });
                        }
                    }
                }

                // Handle Custom Specs
                if (model.CustomSpecs != null && model.CustomSpecs.Any())
                {
                    var customSpecsGroup = await _context.SpecGroups.FirstOrDefaultAsync(g => g.CategoryId == model.CategoryId && g.Name == "Thông số khác");
                    if (customSpecsGroup == null)
                    {
                        customSpecsGroup = new SpecGroup { CategoryId = model.CategoryId, Name = "Thông số khác", DisplayOrder = 99 };
                        _context.SpecGroups.Add(customSpecsGroup);
                        await _context.SaveChangesAsync();
                    }

                    foreach (var customSpec in model.CustomSpecs)
                    {
                        if (!string.IsNullOrWhiteSpace(customSpec.Key) && !string.IsNullOrWhiteSpace(customSpec.Value))
                        {
                            var specKey = await _context.SpecKeys.FirstOrDefaultAsync(k => k.GroupId == customSpecsGroup.Id && k.Name == customSpec.Key);
                            if (specKey == null)
                            {
                                specKey = new SpecKey { GroupId = customSpecsGroup.Id, Name = customSpec.Key, DisplayOrder = 99 };
                                _context.SpecKeys.Add(specKey);
                                await _context.SaveChangesAsync();
                            }

                            product.ProductSpecs.Add(new ProductSpec
                            {
                                SpecKeyId = specKey.Id,
                                Value = customSpec.Value
                            });
                        }
                    }
                }

                var ImageUpload = Request.Form.Files["ImageUpload"];
                if (ImageUpload != null && ImageUpload.Length > 0)
                {
                    string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images");
                    if (!Directory.Exists(uploadsFolder))
                    {
                        Directory.CreateDirectory(uploadsFolder);
                    }
                    string safeFileName = Path.GetFileNameWithoutExtension(ImageUpload.FileName).Replace(" ", "_");
                    safeFileName = System.Text.RegularExpressions.Regex.Replace(safeFileName, @"[^a-zA-Z0-9_\-]", "");
                    string extension = Path.GetExtension(ImageUpload.FileName);
                    string uniqueFileName = Guid.NewGuid().ToString() + "_" + safeFileName + extension;
                    string filePath = Path.Combine(uploadsFolder, uniqueFileName);
                    using (var fileStream = new FileStream(filePath, FileMode.Create))
                    {
                        await ImageUpload.CopyToAsync(fileStream);
                    }
                    product.ImageUrl = uniqueFileName;
                }

                // Handle additional images (gallery)
                var additionalFiles = Request.Form.Files.Where(f => f.Name == "AdditionalImageUploads").ToList();
                if (additionalFiles.Any())
                {
                    var savedNames = new List<string>();
                    foreach (var file in additionalFiles)
                    {
                        if (file.Length > 0)
                        {
                            var savedName = await ProcessUploadedFile(file);
                            if (savedName != null) savedNames.Add(savedName);
                        }
                    }
                    if (savedNames.Any())
                        product.AdditionalImages = string.Join(",", savedNames);
                }

                if (model.ProductColors != null)
                {
                    foreach (var pc in model.ProductColors)
                    {
                        string? colorImageUrl = null;
                        if (pc.ColorImage != null && pc.ColorImage.Length > 0)
                        {
                            colorImageUrl = await ProcessUploadedFile(pc.ColorImage);
                        }

                        product.ProductColors.Add(new ProductColor
                        {
                            ColorName = pc.ColorName,
                            ColorCode = pc.ColorCode,
                            Quantity = pc.Quantity,
                            ImageUrl = colorImageUrl
                        });
                    }
                }

                _context.Add(product);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(AdminIndex));
            }

            // Log ModelState errors for debugging
            foreach (var state in ModelState)
            {
                foreach (var error in state.Value.Errors)
                {
                    System.Diagnostics.Debug.WriteLine($"Error in {state.Key}: {error.ErrorMessage}");
                }
            }

            ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "CategoryName", model.CategoryId);
            return View(model);
        }

        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.WarehouseManager)]
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null || _context.Products == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.ProductColors)
                .Include(p => p.ProductSpecs)
                    .ThenInclude(ps => ps.SpecKey)
                .FirstOrDefaultAsync(p => p.ProductId == id);
            if (product == null)
            {
                return NotFound();
            }

            var model = new ProductViewModel
            {
                ProductId = product.ProductId,
                ProductName = product.ProductName,
                Price = product.Price,
                Quantity = product.Quantity,
                Description = product.Description,
                CategoryId = product.CategoryId,
                ExistingImage = product.ImageUrl,
                ExistingAdditionalImages = string.IsNullOrEmpty(product.AdditionalImages)
                    ? new List<string>()
                    : product.AdditionalImages.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                ProductColors = product.ProductColors.Select(pc => new ProductColorViewModel
                {
                    Id = pc.Id,
                    ProductId = pc.ProductId,
                    ColorName = pc.ColorName,
                    ColorCode = pc.ColorCode,
                    Quantity = pc.Quantity,
                    ExistingImage = pc.ImageUrl
                }).ToList(),
                Specs = product.ProductSpecs.ToDictionary(ps => ps.SpecKeyId, ps => ps.Value),
                CustomSpecs = product.ProductSpecs.Select(ps => new CustomSpecViewModel
                {
                    Key = ps.SpecKey?.Name ?? "Unknown",
                    Value = ps.Value
                }).ToList()
            };

            ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "CategoryName", product.CategoryId);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.WarehouseManager)]
        public async Task<IActionResult> Edit(int id, ProductViewModel model)
        {
            if (id != model.ProductId)
            {
                return NotFound();
            }

            ModelState.Remove("ExistingImage");
            ModelState.Remove("ProductImage");

            // Validation: Must have at least one color
            if (model.ProductColors == null || !model.ProductColors.Any())
            {
                ModelState.AddModelError("ProductColors", "Please add at least one color variant.");
            }

            if (ModelState.IsValid)
            {
                var product = await _context.Products
                    .Include(p => p.ProductColors)
                    .Include(p => p.ProductSpecs)
                    .FirstOrDefaultAsync(p => p.ProductId == id);

                if (product == null)
                {
                    return NotFound();
                }

                // Update basic properties
                product.ProductName = model.ProductName;
                product.Price = model.Price;
                product.Quantity = model.Quantity;
                product.Description = model.Description;
                product.CategoryId = model.CategoryId;

                // --- Sync ProductColors ---
                // 1. Remove colors that are no longer in the model
                var modelColorIds = model.ProductColors.Select(pc => pc.Id).ToList();
                var colorsToRemove = product.ProductColors.Where(pc => !modelColorIds.Contains(pc.Id)).ToList();
                foreach (var color in colorsToRemove)
                {
                    _context.ProductColors.Remove(color);
                }

                // 2. Update existing or add new colors
                foreach (var modelColor in model.ProductColors)
                {
                    var existingColor = product.ProductColors.FirstOrDefault(pc => pc.Id == modelColor.Id && pc.Id != 0);
                    
                    string? colorImageUrl = modelColor.ExistingImage;
                    if (modelColor.ColorImage != null && modelColor.ColorImage.Length > 0)
                    {
                        // Delete old color image if exists
                        if (!string.IsNullOrEmpty(modelColor.ExistingImage))
                        {
                            string oldFilePath = Path.Combine(_webHostEnvironment.WebRootPath, "images", modelColor.ExistingImage);
                            if (System.IO.File.Exists(oldFilePath))
                            {
                                try { System.IO.File.Delete(oldFilePath); } catch { }
                            }
                        }
                        colorImageUrl = await ProcessUploadedFile(modelColor.ColorImage);
                    }

                    if (existingColor != null)
                    {
                        // Update
                        existingColor.ColorName = modelColor.ColorName;
                        existingColor.ColorCode = modelColor.ColorCode;
                        existingColor.Quantity = modelColor.Quantity;
                        existingColor.ImageUrl = colorImageUrl;
                    }
                    else
                    {
                        // Add new
                        product.ProductColors.Add(new ProductColor
                        {
                            ColorName = modelColor.ColorName,
                            ColorCode = modelColor.ColorCode,
                            Quantity = modelColor.Quantity,
                            ProductId = product.ProductId,
                            ImageUrl = colorImageUrl
                        });
                    }
                }

                // --- Sync ProductSpecs ---
                if (model.Specs != null)
                {
                    foreach (var kvp in model.Specs)
                    {
                        var existingSpec = product.ProductSpecs.FirstOrDefault(ps => ps.SpecKeyId == kvp.Key);
                        if (!string.IsNullOrWhiteSpace(kvp.Value))
                        {
                            if (existingSpec != null)
                            {
                                existingSpec.Value = kvp.Value;
                            }
                            else
                            {
                                product.ProductSpecs.Add(new ProductSpec { SpecKeyId = kvp.Key, Value = kvp.Value });
                            }
                        }
                        else if (existingSpec != null)
                        {
                            _context.ProductSpecs.Remove(existingSpec);
                        }
                    }
                }

                // --- Sync Custom Specs ---
                if (model.CustomSpecs != null && model.CustomSpecs.Any())
                {
                    var customSpecsGroup = await _context.SpecGroups.FirstOrDefaultAsync(g => g.CategoryId == model.CategoryId && g.Name == "Thông số khác");
                    if (customSpecsGroup == null)
                    {
                        customSpecsGroup = new SpecGroup { CategoryId = model.CategoryId, Name = "Thông số khác", DisplayOrder = 99 };
                        _context.SpecGroups.Add(customSpecsGroup);
                        await _context.SaveChangesAsync();
                    }

                    foreach (var customSpec in model.CustomSpecs)
                    {
                        if (!string.IsNullOrWhiteSpace(customSpec.Key) && !string.IsNullOrWhiteSpace(customSpec.Value))
                        {
                            var specKey = await _context.SpecKeys.FirstOrDefaultAsync(k => k.GroupId == customSpecsGroup.Id && k.Name == customSpec.Key);
                            if (specKey == null)
                            {
                                specKey = new SpecKey { GroupId = customSpecsGroup.Id, Name = customSpec.Key, DisplayOrder = 99 };
                                _context.SpecKeys.Add(specKey);
                                await _context.SaveChangesAsync();
                            }

                            var existingSpec = product.ProductSpecs.FirstOrDefault(ps => ps.SpecKeyId == specKey.Id);
                            if (existingSpec != null)
                            {
                                existingSpec.Value = customSpec.Value;
                            }
                            else
                            {
                                product.ProductSpecs.Add(new ProductSpec { SpecKeyId = specKey.Id, Value = customSpec.Value });
                            }
                        }
                    }
                }

                if (model.ProductImage != null)
                {
                    if (model.ExistingImage != null)
                    {
                        string oldFilePath = Path.Combine(_webHostEnvironment.WebRootPath, "images", model.ExistingImage);
                        if (System.IO.File.Exists(oldFilePath))
                        {
                            try
                            {
                                System.IO.File.Delete(oldFilePath);
                            }
                            catch (IOException)
                            {
                                System.Diagnostics.Debug.WriteLine($"Could not delete locked file: {oldFilePath}");
                            }
                        }
                    }
                    product.ImageUrl = await ProcessUploadedFile(model.ProductImage);
                }

                // Handle additional images: keep existing minus deleted, then append new uploads
                var keepImages = string.IsNullOrEmpty(product.AdditionalImages)
                    ? new List<string>()
                    : product.AdditionalImages.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

                // Remove images flagged for deletion
                var toDelete = Request.Form["DeleteAdditionalImages"].ToList();
                foreach (var filename in toDelete)
                {
                    keepImages.Remove(filename);
                    string delPath = Path.Combine(_webHostEnvironment.WebRootPath, "images", filename);
                    if (System.IO.File.Exists(delPath))
                    {
                        try { System.IO.File.Delete(delPath); } catch { }
                    }
                }

                // Upload new additional images
                var additionalFiles = Request.Form.Files.Where(f => f.Name == "AdditionalImageUploads").ToList();
                foreach (var file in additionalFiles)
                {
                    if (file.Length > 0)
                    {
                        var savedName = await ProcessUploadedFile(file);
                        if (savedName != null) keepImages.Add(savedName);
                    }
                }

                product.AdditionalImages = keepImages.Any() ? string.Join(",", keepImages) : null;

                try
                {
                    _context.Update(product);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductExists(product.ProductId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(AdminIndex));
            }

            // Log ModelState errors for debugging
            foreach (var state in ModelState)
            {
                foreach (var error in state.Value.Errors)
                {
                    System.Diagnostics.Debug.WriteLine($"Edit Error in {state.Key}: {error.ErrorMessage}");
                }
            }

            ViewData["CategoryId"] = new SelectList(_context.Categories, "CategoryId", "CategoryName", model.CategoryId);
            return View(model);
        }

        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.WarehouseManager)]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null || _context.Products == null)
            {
                return NotFound();
            }

            var product = await _context.Products
                .Include(p => p.Category)
                .FirstOrDefaultAsync(m => m.ProductId == id);
            if (product == null)
            {
                return NotFound();
            }

            return View(product);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.WarehouseManager)]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            if (_context.Products == null)
            {
                return Problem("Entity set 'AppDbContext.Products'  is null.");
            }

            // Check if product is in any orders
            var hasOrders = await _context.OrderDetails.AnyAsync(od => od.ProductId == id);
            if (hasOrders)
            {
                TempData["Error"] = "Không thể xóa sản phẩm này vì đã có khách hàng đặt mua (dữ liệu trong Đơn hàng). Bạn chỉ có thể chỉnh sửa thông tin hoặc xóa các sản phẩm chưa từng được bán.";
                return RedirectToAction(nameof(Delete), new { id = id });
            }

            var product = await _context.Products.FindAsync(id);
            if (product != null)
            {
                if (!string.IsNullOrEmpty(product.ImageUrl))
                {
                    string filePath = Path.Combine(_webHostEnvironment.WebRootPath, "images", product.ImageUrl);
                    if (System.IO.File.Exists(filePath))
                    {
                        try
                        {
                            System.IO.File.Delete(filePath);
                        }
                        catch (IOException)
                        {
                            // Log or ignore, the file is locked
                            System.Diagnostics.Debug.WriteLine($"Could not delete locked file: {filePath}");
                        }
                    }
                }
                _context.Products.Remove(product);
            }
            
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(AdminIndex));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff + "," + UserRoles.WarehouseManager)]
        public async Task<IActionResult> UpdateColorStock(int colorId, int quantity)
        {
            var color = await _context.ProductColors.Include(pc => pc.Product).FirstOrDefaultAsync(pc => pc.Id == colorId);
            if (color != null)
            {
                var oldQty = color.Quantity;
                color.Quantity = quantity;
                
                var email = User.FindFirstValue(ClaimTypes.Email) ?? "System";
                _context.ActivityLogs.Add(new ActivityLog
                {
                    Action = $"Updated stock for {color.Product?.ProductName} ({color.ColorName}): {oldQty} -> {quantity}",
                    UserEmail = email
                });

                await _context.SaveChangesAsync();
            }

            var referer = Request.Headers["Referer"].ToString();
            if (!string.IsNullOrEmpty(referer)) {
                return Redirect(referer);
            }
            return RedirectToAction(nameof(AdminIndex));
        }

        private bool ProductExists(int id)
        {
          return (_context.Products?.Any(e => e.ProductId == id)).GetValueOrDefault();
        }

        private async Task<string?> ProcessUploadedFile(IFormFile? file)
        {
            if (file == null || file.Length == 0) return null;

            string uploadsFolder = Path.Combine(_webHostEnvironment.WebRootPath, "images");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            string safeFileName = Path.GetFileNameWithoutExtension(file.FileName).Replace(" ", "_");
            safeFileName = System.Text.RegularExpressions.Regex.Replace(safeFileName, @"[^a-zA-Z0-9_\-]", "");
            string extension = Path.GetExtension(file.FileName);
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + safeFileName + extension;
            
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            return uniqueFileName;
        }
    }
}
