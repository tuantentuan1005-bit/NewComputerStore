using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using NewComputerStore.Data;
using NewComputerStore.Services;
using NewComputerStore.ViewModels;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace NewComputerStore.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;

        public AdminController(AppDbContext context, IEmailService emailService, IConfiguration configuration, IWebHostEnvironment env)
        {
            _context = context;
            _emailService = emailService;
            _configuration = configuration;
            _env = env;
        }

        public async Task<IActionResult> Index(DateTime? fromDate, DateTime? toDate, string status = "")
        {
            var today = DateTime.Today;
            var startOfMonth = new DateTime(today.Year, today.Month, 1);
            
            var allOrdersQuery = _context.Orders
                                    .Include(o => o.User)
                                    .AsQueryable();

            // 1. Logic doanh thu chuẩn:
            // Đơn QR: IsPaid = true (PaymentStatus == "Paid")
            // Đơn COD: Status = "Delivered"
            // KHÔNG tính Pending, Cancelled, Chưa thanh toán
            var paidOrdersQuery = allOrdersQuery.Where(o => 
                 o.Status != "Cancelled" && o.Status != "Pending" &&
                 (
                    (o.PaymentMethod == "QR" && o.PaymentStatus == "Paid") || 
                    (o.PaymentMethod != "QR" && o.Status == "Delivered")
                 )
            );

            // Table Danh sách đơn đã thanh toán: Có áp dụng bộ lọc (Filter)
            var filteredOrdersQuery = paidOrdersQuery;
            if (fromDate.HasValue) filteredOrdersQuery = filteredOrdersQuery.Where(o => o.OrderDate.Date >= fromDate.Value.Date);
            if (toDate.HasValue) filteredOrdersQuery = filteredOrdersQuery.Where(o => o.OrderDate.Date <= toDate.Value.Date);
            if (!string.IsNullOrEmpty(status)) filteredOrdersQuery = filteredOrdersQuery.Where(o => o.Status == status);

            var paidOrdersList = await filteredOrdersQuery.OrderByDescending(o => o.OrderDate).ToListAsync();

            // Tính doanh thu tổng (Revenue metrics - Không dùng filter ở đây vì Card đại diện cho tổng toàn hệ thống)
            decimal totalRevenue = await paidOrdersQuery.SumAsync(o => (decimal?)o.TotalPrice) ?? 0;
            decimal revenueToday = await paidOrdersQuery.Where(o => o.OrderDate.Date == today).SumAsync(o => (decimal?)o.TotalPrice) ?? 0;
            decimal revenueThisMonth = await paidOrdersQuery.Where(o => o.OrderDate >= startOfMonth).SumAsync(o => (decimal?)o.TotalPrice) ?? 0;

            var startOfLastMonth = new DateTime(today.AddMonths(-1).Year, today.AddMonths(-1).Month, 1);
            var endOfLastMonth = startOfMonth.AddDays(-1);
            decimal revenueLastMonth = await paidOrdersQuery.Where(o => o.OrderDate.Date >= startOfLastMonth.Date && o.OrderDate.Date <= endOfLastMonth.Date).SumAsync(o => (decimal?)o.TotalPrice) ?? 0;
            
            double growth = 0;
            if (revenueLastMonth == 0) {
                growth = revenueThisMonth > 0 ? 100 : 0;
            } else {
                growth = (double)((revenueThisMonth - revenueLastMonth) / revenueLastMonth) * 100;
            }

            // --- Product Sales Analytics ---
            var paidOrderIdsQuery = paidOrdersQuery.Select(o => o.OrderId);
            var validDetails = await _context.OrderDetails
                                .Where(od => paidOrderIdsQuery.Contains(od.OrderId))
                                .GroupBy(od => od.ProductId)
                                .Select(g => new { ProductId = g.Key, TotalSold = g.Sum(x => x.Quantity) })
                                .ToListAsync();

            var allProducts = await _context.Products.Select(p => new { p.ProductId, p.ProductName }).ToListAsync();

            var productSales = allProducts.Select(p => new ItemSales
            {
                ProductName = p.ProductName,
                TotalSold = validDetails.FirstOrDefault(v => v.ProductId == p.ProductId)?.TotalSold ?? 0
            }).ToList();

            var topSelling = productSales.Where(p => p.TotalSold > 0)
                                         .OrderByDescending(p => p.TotalSold)
                                         .Take(5).ToList();

            var leastSelling = productSales.Where(p => p.TotalSold > 0)
                                           .OrderBy(p => p.TotalSold)
                                           .Take(5).ToList();

            var deadStock = productSales.Where(p => p.TotalSold == 0)
                                        .OrderBy(p => p.ProductName)
                                        .ToList();

            // Chart 7 Ngày gần nhất
            var last7Days = Enumerable.Range(0, 7).Select(i => today.AddDays(-i)).Reverse().ToList();
            var chartLabels7Days = last7Days.Select(d => d.ToString("dd/MM")).ToList();
            var chartData7Days = new System.Collections.Generic.List<decimal>();

            var sevenDaysStart = today.AddDays(-6);
            var last7DaysOrders = await paidOrdersQuery
                                    .Where(o => o.OrderDate.Date >= sevenDaysStart)
                                    .ToListAsync();

            foreach (var date in last7Days)
            {
                chartData7Days.Add(last7DaysOrders.Where(o => o.OrderDate.Date == date).Sum(o => o.TotalPrice));
            }

            // Chart 12 Tháng của năm hiện tại
            int currentYear = today.Year;
            var chartLabelsMonths = Enumerable.Range(1, 12).Select(m => $"Tháng {m}").ToList();
            var chartDataMonths = new System.Collections.Generic.List<decimal>();

            var yearOrders = await paidOrdersQuery
                                    .Where(o => o.OrderDate.Year == currentYear)
                                    .ToListAsync();
            
            for (int month = 1; month <= 12; month++)
            {
                chartDataMonths.Add(yearOrders.Where(o => o.OrderDate.Month == month).Sum(o => o.TotalPrice));
            }

            // --- Calculate Email Statistics ---
            // Skipped = EnableEmail was false; they don't count as sent or failed
            int totalEmailsSent    = await _context.EmailLogs.CountAsync(l => l.Status != "Skipped");
            int totalEmailsSuccess = await _context.EmailLogs.CountAsync(l => l.Status == "Success");
            int totalEmailsFailed  = await _context.EmailLogs.CountAsync(l => l.Status == "Failed");
            int totalEmailsSkipped = await _context.EmailLogs.CountAsync(l => l.Status == "Skipped");
            
            int totalUsers = await _context.Users.CountAsync();
            int verifiedUsers = await _context.Users.CountAsync(u => u.IsEmailVerified);
            int unverifiedUsersCount = await _context.Users.CountAsync(u => !u.IsEmailVerified);
            double emailVerificationRate = totalUsers > 0 ? (double)verifiedUsers / totalUsers * 100 : 0;

            // Chart last 30 days success vs failed logs
            var last30Days = Enumerable.Range(0, 30).Select(i => today.AddDays(-i)).Reverse().ToList();
            var chartLabels30Days = last30Days.Select(d => d.ToString("dd/MM")).ToList();
            var chartSuccess30Days = new System.Collections.Generic.List<int>();
            var chartFailed30Days  = new System.Collections.Generic.List<int>();

            var thirtyDaysStart = today.AddDays(-29);
            var last30DaysLogs = await _context.EmailLogs
                .Where(l => l.SentTime.Date >= thirtyDaysStart.Date)
                .ToListAsync();

            foreach (var date in last30Days)
            {
                chartSuccess30Days.Add(last30DaysLogs.Count(l => l.SentTime.Date == date.Date && l.Status == "Success"));
                chartFailed30Days.Add(last30DaysLogs.Count(l  => l.SentTime.Date == date.Date && l.Status == "Failed"));
            }

            var model = new AdminDashboardViewModel
            {
                TotalProducts = await _context.Products.CountAsync(),
                TotalOrders = await _context.Orders.CountAsync(),
                TotalUsers = totalUsers,
                TotalRevenue = totalRevenue,
                RevenueToday = revenueToday,
                RevenueThisMonth = revenueThisMonth,
                RevenueLastMonth = revenueLastMonth,
                RevenueGrowth = growth,
                PaidOrders = paidOrdersList,
                ChartLabels7Days = chartLabels7Days,
                ChartData7Days = chartData7Days,
                ChartLabelsMonths = chartLabelsMonths,
                ChartDataMonths = chartDataMonths,
                TopSellingProducts = topSelling,
                LeastSellingProducts = leastSelling,
                DeadStockProducts = deadStock,

                // Email stats
                TotalEmailsSent     = totalEmailsSent,
                TotalEmailsSuccess  = totalEmailsSuccess,
                TotalEmailsFailed   = totalEmailsFailed,
                TotalEmailsSkipped  = totalEmailsSkipped,
                EmailVerificationRate = emailVerificationRate,
                UnverifiedUsersCount  = unverifiedUsersCount,
                ChartLabels30Days   = chartLabels30Days,
                ChartSuccess30Days  = chartSuccess30Days,
                ChartFailed30Days   = chartFailed30Days
            };

            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.Status = status;

            return View(model);
        }
        public async Task<IActionResult> Logs(int page = 1)
        {
            int pageSize = 15;
            var query = _context.ActivityLogs.OrderByDescending(l => l.Time);
            
            var totalCount = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            
            var logs = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;

            return View(logs);
        }

        public async Task<IActionResult> EmailLogs(string search = "", string status = "", int page = 1)
        {
            int pageSize = 15;
            var query = _context.EmailLogs.AsQueryable();

            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(l => l.Receiver.Contains(search) || l.Subject.Contains(search));
            }

            if (!string.IsNullOrEmpty(status))
            {
                query = query.Where(l => l.Status == status);
            }

            query = query.OrderByDescending(l => l.SentTime);

            var totalCount = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));

            var logs = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.Search = search;
            ViewBag.StatusFilter = status;

            return View(logs);
        }

        [HttpGet]
        public async Task<IActionResult> GetEmailDetails(int id)
        {
            var log = await _context.EmailLogs.FindAsync(id);
            if (log == null)
            {
                return NotFound();
            }
            return Json(new { body = log.Body });
        }

        // ---------------------------------------------------------------
        // EMAIL DIAGNOSTICS
        // ---------------------------------------------------------------

        [HttpGet]
        public IActionResult EmailDiagnostics()
        {
            var smtpStatus = _emailService.GetSmtpStatus();
            ViewBag.SmtpStatus = smtpStatus;
            ViewBag.IsDevelopment = _env.IsDevelopment();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendTestEmail(string toEmail)
        {
            if (string.IsNullOrWhiteSpace(toEmail))
            {
                TempData["TestError"] = "Vui lòng nhập địa chỉ email nhận test.";
                return RedirectToAction(nameof(EmailDiagnostics));
            }

            var result = await _emailService.SendTestEmailAsync(toEmail);

            // Pass result back via TempData-safe approach: ViewData on the same view
            var smtpStatus = _emailService.GetSmtpStatus();
            ViewBag.SmtpStatus = smtpStatus;
            ViewBag.IsDevelopment = _env.IsDevelopment();
            ViewBag.TestResult = result;
            ViewBag.TestEmail = toEmail;
            return View(nameof(EmailDiagnostics));
        }
    }
}
