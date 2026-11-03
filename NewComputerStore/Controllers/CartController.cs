using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using NewComputerStore.Data;
using NewComputerStore.Extensions;
using NewComputerStore.Models;
using NewComputerStore.Services;

namespace NewComputerStore.Controllers
{
    public class CartController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IOrderService _orderService;

        public CartController(AppDbContext context, IOrderService orderService)
        {
            _context = context;
            _orderService = orderService;
        }

        public async Task<IActionResult> Index()
        {
            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();
            var (hasExpiredItems, validationResults) = await _orderService.ValidateAndRefreshCartAsync(cart);

            if (cart.Count > 0)
            {
                HttpContext.Session.Set("Cart", cart);
            }

            ViewBag.ValidationResults = validationResults;
            ViewBag.HasExpiredItems = hasExpiredItems;

            return View(cart);
        }

        [HttpPost]
        public IActionResult AddToCart(int productId, int? colorId, int quantity = 1)
        {
            if (!colorId.HasValue)
            {
                return BadRequest("Please select a color");
            }

            int selectedColorId = colorId.Value;
            var product = _context.Products.Find(productId);
            if (product != null)
            {
                var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();

                // If colorId is provided, find the color info
                ProductColor? selectedColor = _context.ProductColors.FirstOrDefault(pc => pc.Id == selectedColorId && pc.ProductId == productId);

                // Unique key is ProductId AND ColorId
                var existingItem = cart.FirstOrDefault(c => c.ProductId == productId && c.ColorId == selectedColorId);

                if (existingItem != null)
                {
                    existingItem.Quantity += quantity;
                }
                else
                {
                    var (originalPrice, effectiveDiscount, finalPrice) = _orderService.CalculateProductPrice(product);
                    cart.Add(new CartItem
                    {
                        ProductId = product.ProductId,
                        ProductName = product.ProductName,
                        Price = finalPrice,
                        OriginalPrice = originalPrice,
                        DiscountPercent = effectiveDiscount,
                        Quantity = quantity,
                        ColorId = selectedColorId,
                        ColorName = selectedColor?.ColorName,
                        ColorCode = selectedColor?.ColorCode,
                        ImageUrl = !string.IsNullOrEmpty(selectedColor?.ImageUrl) ? selectedColor.ImageUrl : product.ImageUrl
                    });
                }

                HttpContext.Session.Set("Cart", cart);
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public IActionResult UpdateCart(int productId, int? colorId, int quantity)
        {
            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();
            var item = cart.FirstOrDefault(c => c.ProductId == productId && c.ColorId == colorId);

            if (item != null)
            {
                if (quantity > 0)
                {
                    item.Quantity = quantity;
                }
                else
                {
                    cart.Remove(item);
                }
                HttpContext.Session.Set("Cart", cart);
            }

            return RedirectToAction("Index");
        }

        public IActionResult RemoveFromCart(int productId, int? colorId)
        {
            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();
            var item = cart.FirstOrDefault(c => c.ProductId == productId && c.ColorId == colorId);

            if (item != null)
            {
                cart.Remove(item);
                HttpContext.Session.Set("Cart", cart);
            }

            return RedirectToAction("Index");
        }
    }
}
