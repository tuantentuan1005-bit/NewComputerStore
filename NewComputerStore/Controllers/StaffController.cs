using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewComputerStore.Data;
using NewComputerStore.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NewComputerStore.Controllers
{
    [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff + "," + UserRoles.WarehouseManager + "," + UserRoles.EmployeeManager)]
    public class StaffController : Controller
    {
        private readonly AppDbContext _context;

        public StaffController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Dashboard()
        {
            var today = DateTime.Today;

            // Stats
            ViewBag.OrdersToday = await _context.Orders
                .Where(o => o.OrderDate >= today)
                .CountAsync();

            ViewBag.RevenueToday = await _context.Orders
                .Where(o => o.OrderDate >= today && o.Status != "Cancelled")
                .SumAsync(o => (decimal?)o.TotalPrice) ?? 0;

            // Today's Revenue Orders
            ViewBag.TodayOrders = await _context.Orders
                .Where(o => o.OrderDate >= today && o.Status != "Cancelled")
                .OrderByDescending(o => o.OrderDate)
                .ToListAsync();

            // Product Colors for Stock Management
            var stock = await _context.ProductColors
                .Include(pc => pc.Product)
                .OrderBy(pc => pc.Product.ProductName)
                .ToListAsync();

            return View(stock);
        }
    }
}
