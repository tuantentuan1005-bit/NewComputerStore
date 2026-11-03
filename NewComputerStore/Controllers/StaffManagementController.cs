using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
    [Authorize(Roles = UserRoles.Admin + "," + UserRoles.EmployeeManager)]
    [Route("Admin/Staff")]
    public class StaffManagementController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IPasswordHasher<User> _passwordHasher;
        private const string PRIMARY_ADMIN_EMAIL = "admin@store.com";

        public StaffManagementController(AppDbContext context, IPasswordHasher<User> passwordHasher)
        {
            _context        = context;
            _passwordHasher = passwordHasher;
        }

        // GET /Admin/Staff
        [HttpGet("")]
        [HttpGet("Index")]
        public async Task<IActionResult> Index(int page = 1, string search = "", string role = "", string status = "")
        {
            int pageSize = 10;

            // All non-customer users
            var query = _context.Users
                .Where(u => u.Role != UserRoles.Customer)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
                query = query.Where(u =>
                    u.FullName.Contains(search) ||
                    u.Email.Contains(search));

            if (!string.IsNullOrWhiteSpace(role))
                query = query.Where(u => u.Role == role);

            if (status == "active")
                query = query.Where(u => u.IsActive);
            else if (status == "locked")
                query = query.Where(u => !u.IsActive);

            var totalCount = await query.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));

            var staff = await query
                .OrderByDescending(u => u.UserId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage   = page;
            ViewBag.TotalPages    = totalPages;
            ViewBag.Search        = search;
            ViewBag.RoleFilter    = role;
            ViewBag.Status        = status;
            ViewBag.TotalCount    = totalCount;
            ViewBag.StaffRoles    = UserRoles.AssignableStaffRoles;

            return View(staff);
        }

        // GET /Admin/Staff/Create
        [HttpGet("Create")]
        public IActionResult Create()
        {
            return View();
        }

        // POST /Admin/Staff/Create
        [HttpPost("Create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(string fullName, string email, string password, string role)
        {
            if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(role))
            {
                ModelState.AddModelError("", "All fields are required.");
                return View();
            }

            if (!UserRoles.AssignableStaffRoles.Contains(role))
            {
                ModelState.AddModelError("role", "Invalid role selected.");
                return View();
            }

            var exists = await _context.Users.AnyAsync(u => u.Email == email);
            if (exists)
            {
                ModelState.AddModelError("email", "An account with this email already exists.");
                return View();
            }

            var newUser = new User
            {
                FullName = fullName,
                Email    = email,
                Role     = role,
                IsActive = true
            };
            newUser.Password = _passwordHasher.HashPassword(newUser, password);

            _context.Users.Add(newUser);

            var adminEmail = User.FindFirstValue(ClaimTypes.Email) ?? "System";
            _context.ActivityLogs.Add(new ActivityLog
            {
                Action    = $"Created staff account: {email} with role [{role}]",
                UserEmail = adminEmail
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Staff account '{email}' created successfully with role [{role}].";
            return RedirectToAction(nameof(Index));
        }

        // GET /Admin/Staff/Edit/5
        [HttpGet("Edit/{id}")]
        public async Task<IActionResult> Edit(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null || user.Role == UserRoles.Customer) return NotFound();
            return View(user);
        }

        // POST /Admin/Staff/Edit/5
        [HttpPost("Edit/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, string fullName, string role, bool isActive)
        {
            var existingUser = await _context.Users.FindAsync(id);
            if (existingUser == null || existingUser.Role == UserRoles.Customer)
                return NotFound();

            // Prevent modification of primary admin
            if (existingUser.Email == PRIMARY_ADMIN_EMAIL)
            {
                if (role != UserRoles.Admin || !isActive)
                {
                    TempData["Error"] = "Cannot change role or lock the primary system administrator.";
                    return RedirectToAction(nameof(Index));
                }
            }

            if (!UserRoles.AssignableStaffRoles.Contains(role))
            {
                ModelState.AddModelError("", "Invalid role.");
                return View(existingUser);
            }

            var oldRole    = existingUser.Role;
            var oldActive  = existingUser.IsActive;

            existingUser.FullName = fullName;
            existingUser.Role     = role;
            existingUser.IsActive = existingUser.Email == PRIMARY_ADMIN_EMAIL ? true : isActive;

            var adminEmail = User.FindFirstValue(ClaimTypes.Email) ?? "System";
            _context.ActivityLogs.Add(new ActivityLog
            {
                Action    = $"Edited staff {existingUser.Email}: Role [{oldRole}→{role}], Active [{oldActive}→{existingUser.IsActive}]",
                UserEmail = adminEmail
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Staff '{existingUser.Email}' updated successfully.";
            return RedirectToAction(nameof(Index));
        }

        // POST /Admin/Staff/Delete/5
        [HttpPost("Delete/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null || user.Role == UserRoles.Customer) return NotFound();

            if (user.Email == PRIMARY_ADMIN_EMAIL)
            {
                TempData["Error"] = "Cannot delete the primary administrator account.";
                return RedirectToAction(nameof(Index));
            }

            // Prevent self-deletion
            var currentEmail = User.FindFirstValue(ClaimTypes.Email);
            if (user.Email == currentEmail)
            {
                TempData["Error"] = "You cannot delete your own account.";
                return RedirectToAction(nameof(Index));
            }

            var adminEmail = User.FindFirstValue(ClaimTypes.Email) ?? "System";
            _context.ActivityLogs.Add(new ActivityLog
            {
                Action    = $"Deleted staff account: {user.Email} (Role: {user.Role})",
                UserEmail = adminEmail
            });

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Staff account '{user.Email}' has been deleted.";
            return RedirectToAction(nameof(Index));
        }

        // POST /Admin/Staff/ToggleActive/5
        [HttpPost("ToggleActive/{id}")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var user = await _context.Users.FindAsync(id);
            if (user == null || user.Role == UserRoles.Customer) return NotFound();

            if (user.Email == PRIMARY_ADMIN_EMAIL)
            {
                TempData["Error"] = "Cannot lock the primary administrator.";
                return RedirectToAction(nameof(Index));
            }

            user.IsActive = !user.IsActive;

            var adminEmail = User.FindFirstValue(ClaimTypes.Email) ?? "System";
            var actionStr  = user.IsActive ? "Unlocked" : "Locked";
            _context.ActivityLogs.Add(new ActivityLog
            {
                Action    = $"{actionStr} staff account: {user.Email}",
                UserEmail = adminEmail
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Staff '{user.Email}' has been {actionStr.ToLower()}.";
            return RedirectToAction(nameof(Index));
        }
    }
}
