
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using PSS_Time_Tracker.Models;
using PSS_Time_Tracker.Data;

namespace PSS_Time_Tracker.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly timeSheetRecorderContext _context;

        public HomeController(
            ILogger<HomeController> logger,
            timeSheetRecorderContext context)
        {
            _logger = logger;
            _context = context;
        }

        public IActionResult Index()
        {
            // User provisioning (originally done here via a Graph call the first time
            // someone signed in) now happens up front in AccountController.Login, since the
            // local test accounts are already fully populated in the Users table.
            return View();
        }

        // Kept so the "no supervisor" check still works exactly like before, just reading
        // the local Users table instead of asking Microsoft Graph.
        [HttpGet]
        public async Task<IActionResult> CheckSupervisor()
        {
            try
            {
                var userId = User.GetUserId();
                var user = await _context.Users.FindAsync(userId);

                if (user == null)
                {
                    return Json(new { hasSupervisor = false });
                }

                bool hasSupervisor = !string.IsNullOrWhiteSpace(user.SupervisorFullName) &&
                                    !user.SupervisorFullName.Equals(".", StringComparison.OrdinalIgnoreCase) &&
                                    !user.SupervisorFullName.Equals("N/A", StringComparison.OrdinalIgnoreCase);

                return Json(new { hasSupervisor = hasSupervisor });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking supervisor status");
                return Json(new { hasSupervisor = false });
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
