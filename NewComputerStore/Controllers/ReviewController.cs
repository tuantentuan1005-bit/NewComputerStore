using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewComputerStore.Data;
using NewComputerStore.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Linq;
using System.Threading.Tasks;

namespace NewComputerStore.Controllers
{
    [Authorize]
    public class ReviewController : Controller
    {
        private readonly AppDbContext _context;

        public ReviewController(AppDbContext context)
        {
            _context = context;
        }

        [Authorize(Roles = UserRoles.Admin + "," + UserRoles.Staff + "," + UserRoles.CustomerSupport)]
        public IActionResult Index()
        {
            var reviews = _context.Reviews
                .Include(r => r.Product)
                .Include(r => r.User)
                .OrderByDescending(r => r.ReviewId)
                .ToList();
            return View(reviews);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(int productId, int rating, string comment)
        {
            if (rating < 1 || rating > 5)
            {
                return BadRequest("Invalid rating.");
            }

            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier));

            var review = new Review
            {
                ProductId = productId,
                UserId = userId,
                Rating = rating,
                Comment = comment
            };

            _context.Reviews.Add(review);
            await _context.SaveChangesAsync();

            return RedirectToAction("Details", "Product", new { id = productId });
        }

        [Authorize(Roles = UserRoles.Admin)]
        public async Task<IActionResult> Delete(int id)
        {
            var review = await _context.Reviews.FindAsync(id);
            if (review != null)
            {
                _context.Reviews.Remove(review);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
