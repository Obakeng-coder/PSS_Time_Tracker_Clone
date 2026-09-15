using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using PSS_Time_Tracker.Services;

namespace PSS_Time_Tracker.Controllers
{
    /// <summary>Backs the notification bell in _Layout.cshtml (see
    /// Views/Shared/Components/NotificationBell) - marking things read and following a notification's
    /// link. Every action is scoped to the current user's own notifications only.</summary>
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly INotificationService _notificationService;

        public NotificationsController(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkAllRead()
        {
            var userId = User.GetUserId();
            if (!string.IsNullOrEmpty(userId))
            {
                await _notificationService.MarkAllReadAsync(userId);
            }
            return Redirect(Request.Headers.Referer.ToString() is { Length: > 0 } referer ? referer : "/");
        }

        /// <summary>Marks one notification read, then follows its link - the click target for a
        /// notification in the dropdown. Deliberately a plain GET (no antiforgery token) rather than a
        /// form POST: it's a single in-app anchor click, the same low-stakes "mark my own row read"
        /// mutation MarkAllRead already allows, and a real &lt;a href&gt; is the one interactive element
        /// that can never be intercepted or swallowed by other JS on the page (forms nested inside the
        /// notification dropdown were unreliable - some clicks silently went nowhere).</summary>
        [HttpGet]
        public async Task<IActionResult> Open(int id, string? url)
        {
            var userId = User.GetUserId();
            if (!string.IsNullOrEmpty(userId))
            {
                await _notificationService.MarkReadAsync(id, userId);
            }

            // Only ever follow a relative in-app path - never an absolute/external URL, even though
            // every Url this app writes today is already relative (see INotificationService callers).
            if (!string.IsNullOrWhiteSpace(url) && url.StartsWith('/') && !url.StartsWith("//"))
            {
                return Redirect(url);
            }
            return RedirectToAction("Index", "Home");
        }
    }
}
