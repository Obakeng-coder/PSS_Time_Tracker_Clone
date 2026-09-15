using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PSS_Time_Tracker.Data;

namespace PSS_Time_Tracker.Controllers
{
    // Stands in for the old Azure AD sign-in/sign-out endpoints
    // (Microsoft.Identity.Web.UI's /MicrosoftIdentity/Account/SignIn).
    // Instead of redirecting to Azure AD, this picks one of the seeded local
    // test accounts and issues a cookie for it directly.
    public class AccountController : Controller
    {
        private readonly timeSheetRecorderContext _context;
        private readonly IConfiguration _configuration;

        public AccountController(timeSheetRecorderContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpGet]
        public async Task<IActionResult> Login(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            ViewBag.Users = await _context.Users
                .OrderBy(u => u.IsManager) // employees first, managers last
                .ThenBy(u => u.EmployeeName)
                .ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string azureAdUserId, string? returnUrl = null)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.AzureAdUserId == azureAdUserId);
            if (user == null)
            {
                ModelState.AddModelError("", "Select a valid test account.");
                ViewBag.ReturnUrl = returnUrl;
                ViewBag.Users = await _context.Users.OrderBy(u => u.IsManager).ThenBy(u => u.EmployeeName).ToListAsync();
                return View();
            }

            // This claim set stands in for what Azure AD used to hand us in the OIDC id_token:
            // a stable user id, a display name, an email/UPN, and (for managers) a "groups"
            // claim carrying the manager-group id that the "RequireManagerRole" policy checks.
            // Note: the claim type is the literal string "name" (not ClaimTypes.Name's long URI)
            // because that's what the existing Views check for (c.Type == "name"), matching the
            // raw "name" claim Azure AD's OIDC token used to carry.
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.AzureAdUserId),
                new Claim("name", $"{user.EmployeeName} {user.EmployeeSurname}"),
                new Claim("preferred_username", user.Email),
                new Claim("email", user.Email),
            };

            if (user.IsManager)
            {
                claims.Add(new Claim("groups", _configuration["ManagerGroupId"] ?? "local-managers"));
            }

            if (user.IsHr)
            {
                claims.Add(new Claim("groups", _configuration["HrGroupId"] ?? "local-hr"));
            }

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme, nameType: "name", roleType: ClaimTypes.Role);
            var principal = new ClaimsPrincipal(identity);

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SignOut()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Login");
        }
    }
}
