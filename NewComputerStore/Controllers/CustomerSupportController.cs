using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NewComputerStore.Models;

namespace NewComputerStore.Controllers
{
    [Authorize(Roles = UserRoles.CustomerSupport + "," + UserRoles.Admin)]
    public class CustomerSupportController : Controller
    {
        public IActionResult Dashboard()
        {
            // For now, redirect to the main chat dashboard as it's the primary tool
            return RedirectToAction("StaffDashboard", "Chat");
        }
    }
}
