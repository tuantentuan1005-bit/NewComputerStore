using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewComputerStore.Models;

namespace NewComputerStore.Controllers
{
    /// <summary>
    /// Legacy redirect: /Admin/Users now redirects to /Admin/Customers.
    /// Kept so old bookmarks don't 404.
    /// </summary>
    [Authorize(Roles = UserRoles.Admin)]
    [Route("Admin/Users")]
    public class UserController : Controller
    {
        [HttpGet("")]
        [HttpGet("Index")]
        public IActionResult Index()
            => RedirectToAction("Index", "Customer");
    }
}
