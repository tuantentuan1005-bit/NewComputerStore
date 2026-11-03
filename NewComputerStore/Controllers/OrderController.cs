using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewComputerStore.Data;
using NewComputerStore.Extensions;
using NewComputerStore.Models;
using NewComputerStore.Services;
using NewComputerStore.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace NewComputerStore.Controllers
{
    [Authorize]
    public class OrderController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IOrderService _orderService;

        public OrderController(AppDbContext context, IEmailService emailService, IOrderService orderService)
        {
            _context = context;
            _emailService = emailService;
            _orderService = orderService;
        }

        // --- CUSTOMER FEATURES ---
        public async Task<IActionResult> Index()
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var orders = await _context.Orders
                .Where(o => o.UserId == userId)
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            return View(orders);
        }

        public async Task<IActionResult> Details(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            
            var order = await _orderService.GetOrderByIdWithDetailsAsync(id);

            if (order == null)
            {
                return NotFound();
            }

            // check ownership or admin/staff/support
            if (order.UserId != userId && !User.IsInRole("Admin") && !User.IsInRole("Staff"))
            {
                return Forbid();
            }

            return View(order);
        }

        public async Task<IActionResult> Checkout()
        {
            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();
            if (cart.Count == 0)
            {
                return RedirectToAction("Index", "Cart");
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var user = await _context.Users.FindAsync(userId);

            // Recheck expired/disabled items and refresh DB prices via shared IOrderService
            var (hasExpiredItems, _) = await _orderService.ValidateAndRefreshCartAsync(cart);
            if (hasExpiredItems)
            {
                TempData["Error"] = "Giỏ hàng của bạn chứa sản phẩm đã hết hàng hoặc ngừng kinh doanh. Vui lòng cập nhật lại giỏ hàng trước khi đặt hàng.";
                return RedirectToAction("Index", "Cart");
            }

            HttpContext.Session.Set("Cart", cart);

            var model = new CheckoutViewModel
            {
                FullName = user?.FullName ?? "",
                Email = user?.Email ?? "",
                TotalAmount = cart.Sum(c => c.Total)
            };

            ViewBag.Cart = cart;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(CheckoutViewModel model)
        {
            var cart = HttpContext.Session.Get<List<CartItem>>("Cart") ?? new List<CartItem>();
            if (cart.Count == 0)
            {
                return RedirectToAction("Index", "Cart");
            }

            if (ModelState.IsValid)
            {
                var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
                var command = new CreateOrderCommand
                {
                    UserId = userId,
                    FullName = model.FullName,
                    Email = model.Email,
                    Phone = model.Phone,
                    Address = model.Address,
                    PaymentMethod = model.PaymentMethod == "QR" ? "QR" : "COD",
                    Items = cart.Select(c => new CreateOrderItemInput
                    {
                        ProductId = c.ProductId,
                        ColorId = c.ColorId,
                        Quantity = c.Quantity
                    }).ToList()
                };

                var result = await _orderService.CreateOrderAsync(command);
                if (!result.Succeeded || result.Order == null)
                {
                    ModelState.AddModelError("", result.Message);
                    ViewBag.Cart = cart;
                    return View(model);
                }

                // Clear cart
                HttpContext.Session.Remove("Cart");

                // Redirect based on payment method
                if (result.Order.PaymentMethod == "QR")
                {
                    return RedirectToAction("PaymentQr", new { id = result.Order.OrderId });
                }

                return RedirectToAction("CheckoutSuccess", new { id = result.Order.OrderId });
            }

            ViewBag.Cart = cart;
            return View(model);
        }

        public IActionResult CheckoutSuccess(int id)
        {
            ViewBag.OrderId = id;
            return View();
        }

        // QR Payment Page - show QR code to customer
        public async Task<IActionResult> PaymentQr(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var order = await _context.Orders.FindAsync(id);

            if (order == null) return NotFound();
            if (order.UserId != userId && !User.IsInRole("Admin")) return Forbid();
            if (order.PaymentMethod != "QR") return RedirectToAction("Details", new { id });

            return View(order);
        }

        // Customer marks payment as sent
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAsPaid(int id)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));
            var order = await _context.Orders.FindAsync(id);

            if (order == null) return NotFound();
            if (order.UserId != userId) return Forbid();

            // Flag that customer claims payment done - Admin will verify
            order.Note = $"Customer claimed payment at {DateTime.Now:dd/MM/yyyy HH:mm}";
            await _context.SaveChangesAsync();

            TempData["PaymentNotice"] = "Cảm ơn! Chúng tôi sẽ xác nhận thanh toán của bạn sớm nhất có thể.";
            return RedirectToAction("Details", new { id });
        }

        // --- ADMIN FEATURES ---

        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff)]
        public async Task<IActionResult> AdminIndex(string statusFilter)
        {
            var query = _context.Orders
                .Include(o => o.User)
                .AsQueryable();

            if (!string.IsNullOrEmpty(statusFilter))
            {
                query = query.Where(o => o.Status == statusFilter);
            }

            var orders = await query
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            ViewBag.StatusFilter = statusFilter;
            return View(orders);
        }

        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff)]
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int orderId, string status)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order != null)
            {
                var referer = Request.Headers["Referer"].ToString();
                string redirectUrl = string.IsNullOrEmpty(referer) ? Url.Action(nameof(AdminIndex)) : referer;

                if (order.PaymentMethod == "QR" && order.PaymentStatus != "Paid" && status != "Cancelled" && status != "Pending")
                {
                    TempData["Error"] = "Đơn hàng thanh toán QR chưa được xác nhận thanh toán. Không thể chuyển lên trạng thái tiếp theo.";
                    return Redirect(redirectUrl);
                }

                int GetLevel(string s) => s switch {
                    "Pending" => 0,
                    "Processing" => 1,
                    "Shipped" => 2,
                    "Delivered" => 3,
                    _ => -1
                };

                var oldLevel = GetLevel(order.Status);
                var newLevel = GetLevel(status);

                // Allow Cancelled and Refunded statuses to skip sequential level checking
                if (status != "Cancelled" && status != "Refunded" && newLevel != oldLevel && newLevel != oldLevel + 1)
                {
                    TempData["Error"] = "Trạng thái đơn hàng phải được cập nhật tuần tự (Pending -> Processing -> Shipped -> Delivered).";
                    return Redirect(redirectUrl);
                }

                var oldStatus = order.Status;
                order.Status = status;

                var email = User.FindFirstValue(ClaimTypes.Email) ?? "System";
                _context.ActivityLogs.Add(new ActivityLog
                {
                    Action = $"Updated Order #{orderId} status ({oldStatus}->{status})",
                    UserEmail = email
                });

                await _context.SaveChangesAsync();

                // Trigger Order Status Email notification
                try
                {
                    var fullyPopulatedOrder = await _context.Orders
                        .Include(o => o.User)
                        .Include(o => o.OrderDetails)
                        .ThenInclude(od => od.Product)
                        .FirstOrDefaultAsync(o => o.OrderId == order.OrderId);

                    if (fullyPopulatedOrder != null && fullyPopulatedOrder.User != null)
                    {
                        await _emailService.SendOrderStatusEmailAsync(fullyPopulatedOrder.User.Email, fullyPopulatedOrder, status);
                    }
                }
                catch (Exception)
                {
                    // Catch gracefully to keep transaction unaffected
                }

                TempData["Success"] = $"Cập nhật trạng thái đơn hàng #{orderId} thành công.";
                return Redirect(redirectUrl);
            }

            return NotFound();
        }

        // ADMIN: Confirm payment received
        [Authorize(Roles = UserRoles.Admin)]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmPayment(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            order.PaymentStatus = "Paid";
            order.Status = "Processing"; // Move order forward

            var email = User.FindFirstValue(ClaimTypes.Email) ?? "Admin";
            _context.ActivityLogs.Add(new ActivityLog
            {
                Action = $"Confirmed QR Payment for Order #{id}",
                UserEmail = email
            });

            await _context.SaveChangesAsync();

            // Trigger OrderConfirmed (Processing) email
            try
            {
                var fullyPopulatedOrder = await _context.Orders
                    .Include(o => o.User)
                    .Include(o => o.OrderDetails)
                    .ThenInclude(od => od.Product)
                    .FirstOrDefaultAsync(o => o.OrderId == order.OrderId);

                if (fullyPopulatedOrder != null && fullyPopulatedOrder.User != null)
                {
                    await _emailService.SendOrderStatusEmailAsync(fullyPopulatedOrder.User.Email, fullyPopulatedOrder, "Processing");
                }
            }
            catch (Exception)
            {
                // Catch gracefully
            }

            return RedirectToAction(nameof(AdminIndex));
        }
    }
}
