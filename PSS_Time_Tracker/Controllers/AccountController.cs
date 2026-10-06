using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Mvc;

namespace PSS_Time_Tracker.Controllers
{
    // Sign-in is Azure AD only (Microsoft.Identity.Web, wired up in Program.cs) - the mock
    // dropdown of seeded local test accounts this controller used to offer is gone; every
    // employee's access now comes through the real providencesoft.com tenant. Login sends
    // straight into the OIDC challenge (true single-sign-on behavior - there's only one real way
    // in, so there's nothing to choose between); it only actually renders a page when there's
    // something to say (Azure AD not configured at all, or a sign-in attempt just failed).
    // Program.cs's OnTokenValidated event does the real work: looking the signed-in person up in
    // Users by email and building the app's own claims from AppIdentityFactory.
    public class AccountController : Controller
    {
        private readonly IConfiguration _configuration;

        public AccountController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private bool AzureAdConfigured =>
            !string.IsNullOrWhiteSpace(_configuration["AzureAd:ClientId"]) &&
            !string.IsNullOrWhiteSpace(_configuration["AzureAd:TenantId"]);

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            var authError = Request.Query["authError"].FirstOrDefault();

            // Nothing to choose and nothing to say - go straight to Microsoft. Only stop here (and
            // render the view below) when Azure AD isn't configured, or the last attempt just failed
            // and needs to be shown before trying again.
            if (AzureAdConfigured && string.IsNullOrEmpty(authError))
            {
                return RedirectToAction(nameof(AzureLogin), new { returnUrl });
            }

            ViewBag.ReturnUrl = returnUrl;
            ViewBag.AzureAdConfigured = AzureAdConfigured;
            ViewBag.AuthError = authError;
            return View();
        }

        [HttpGet]
        public IActionResult AzureLogin(string? returnUrl = null)
        {
            if (!AzureAdConfigured)
            {
                return RedirectToAction(nameof(Login), new { returnUrl });
            }

            var redirectUri = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                ? returnUrl
                : Url.Action("Index", "Home");

            return Challenge(
                new AuthenticationProperties { RedirectUri = redirectUri },
                OpenIdConnectDefaults.AuthenticationScheme);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult SignOut()
        {
            var properties = new AuthenticationProperties { RedirectUri = Url.Action(nameof(Login)) };

            // A federated sign-out (through Microsoft's own logout endpoint too, via the
            // OpenIdConnect scheme) - not just clearing our own cookie - or the next protected page
            // gets silently re-authenticated against Azure's still-live session before anyone sees
            // the login screen. Only when Azure AD is actually configured - the OpenIdConnect scheme
            // isn't even registered in the (no-Azure-configured) fallback, so signing out of it there
            // would throw rather than just no-op.
            return AzureAdConfigured
                ? SignOut(properties, CookieAuthenticationDefaults.AuthenticationScheme, OpenIdConnectDefaults.AuthenticationScheme)
                : SignOut(properties, CookieAuthenticationDefaults.AuthenticationScheme);
        }
    }
}
