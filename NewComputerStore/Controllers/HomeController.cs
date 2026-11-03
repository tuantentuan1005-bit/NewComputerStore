using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NewComputerStore.Data;
using System.Linq;
using System.Threading.Tasks;

namespace NewComputerStore.Controllers
{
    public class HomeController : Controller
    {
        private readonly AppDbContext _context;

        public HomeController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Fetch 8 latest products for homepage showcase
            var latestProducts = await _context.Products
                .Include(p => p.Category)
                .Where(p => !p.IsHidden && !p.IsDeleted)
                .OrderByDescending(p => p.ProductId)
                .Take(8)
                .ToListAsync();

            return View(latestProducts);
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View();
        }
    }
}
