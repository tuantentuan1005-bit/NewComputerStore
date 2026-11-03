using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewComputerStore.Data;
using NewComputerStore.Models;
using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace NewComputerStore.Controllers
{
    [Authorize(Roles = UserRoles.Admin + "," + UserRoles.EmployeeManager + "," + UserRoles.CustomerSupport)]
    [Route("Admin/Customers")]
    public class CustomerController : Controller
    {
        private readonly AppDbContext _context;
        private const string PRIMARY_ADMIN_EMAIL = "admin@store.com";

        public CustomerController(AppDbContext context)
        {
            _context = context;
        }

        // GET /Admin/Customers
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index(int page = 1, string search = "", string status = "")
        {
            int pageSize = 10;

            var query = _context.Users
                .Where(u => u.Role == UserRoles.Customer && !u.IsDeleted)
                .AsQueryable();

            // Search filter
            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(u =>
                    u.FullName.Contains(search) ||
                    u.Email.Contains(search));

            // Status filter
            if (status == "active")
                query = query.Where(u => u.IsActive);
            else if (status == "locked")
                query = query.Where(u => !u.IsActive);

            var totalCount  = await query.CountAsync();
            var totalPages  = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));

            var customers = await query
                .OrderByDescending(u => u.UserId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages  = totalPages;
            ViewBag.Search      = search;
            ViewBag.Status      = status;
            ViewBag.TotalCount  = totalCount;

            return View(customers);
        }

        // GET /Admin/Customers/Details/5
        [HttpGet("Details/{id}")]
        public async Task<IActionResult> Details(int id)
        {
            var customer = await _context.Users
                .Include(u => u.Orders)
                .FirstOrDefaultAsync(u => u.UserId == id && u.Role == UserRoles.Customer);

            if (customer == null) return NotFound();
            return View(customer);
        }

        // POST /Admin/Customers/ToggleActive/5
        [HttpPost("ToggleActive/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id, string returnSearch = "", string returnStatus = "", int returnPage = 1)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) return NotFound();

            if (user.Email == PRIMARY_ADMIN_EMAIL)
            {
                TempData["Error"] = "Cannot lock the primary administrator account.";
                return RedirectToAction(nameof(Index), new { page = returnPage, search = returnSearch, status = returnStatus });
            }

            user.IsActive = !user.IsActive;

            var adminEmail = User.FindFirstValue(ClaimTypes.Email) ?? "System";
            var actionStr  = user.IsActive ? "Unlocked" : "Locked";
            _context.ActivityLogs.Add(new ActivityLog
            {
                Action    = $"{actionStr} customer account: {user.Email}",
                UserEmail = adminEmail
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Customer {user.Email} has been {actionStr.ToLower()}.";
            return RedirectToAction(nameof(Index), new { page = returnPage, search = returnSearch, status = returnStatus });
        }

        // POST /Admin/Customers/Delete/5
        [HttpPost("Delete/{id}")]
        [Authorize(Roles = UserRoles.Admin)]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null) 
                return Json(new { success = false, message = "Không tìm thấy khách hàng." });

            if (user.Role != UserRoles.Customer)
            {
                return Json(new { success = false, message = "Không được phép xóa tài khoản quản trị viên hoặc nhân viên hệ thống." });
            }

            var hasOrders = await _context.Orders.AnyAsync(o => o.UserId == user.UserId);
            string deleteType;

            if (hasOrders)
            {
                // Soft Delete
                user.IsDeleted = true;
                _context.Users.Update(user);
                deleteType = "Soft-deleted";
            }
            else
            {
                // Hard Delete
                // Remove reviews
                var reviews = await _context.Reviews.Where(r => r.UserId == user.UserId).ToListAsync();
                if (reviews.Any()) _context.Reviews.RemoveRange(reviews);

                // Remove chat conversations & messages
                var chatConversations = await _context.ChatConversations.Where(c => c.CustomerId == user.UserId).ToListAsync();
                if (chatConversations.Any())
                {
                    foreach (var convo in chatConversations)
                    {
                        var messages = await _context.ChatMessages.Where(m => m.ConversationId == convo.Id).ToListAsync();
                        if (messages.Any()) _context.ChatMessages.RemoveRange(messages);
                    }
                    _context.ChatConversations.RemoveRange(chatConversations);
                }

                // Remove user
                _context.Users.Remove(user);
                deleteType = "Hard-deleted";
            }

            // Write Activity Log
            var adminEmail = User.FindFirstValue(ClaimTypes.Email) ?? "System";
            _context.ActivityLogs.Add(new ActivityLog
            {
                Action    = $"{deleteType} customer account: {user.Email}",
                UserEmail = adminEmail
            });

            await _context.SaveChangesAsync();

            return Json(new { success = true, message = $"Đã xóa khách hàng {user.FullName} ({user.Email}) thành công." });
        }
    }
}
