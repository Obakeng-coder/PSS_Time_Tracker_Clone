using Microsoft.AspNetCore.Mvc;
using PSS_Time_Tracker;
using PSS_Time_Tracker.Services;

namespace PSS_Time_Tracker.ViewComponents
{
    /// <summary>Renders the notification bell in _Layout.cshtml - unread count + the current user's
    /// most recent notifications. Invoked on every authenticated page load (see @await
    /// Component.InvokeAsync in the layout), so this stays a lightweight single query.</summary>
    public class NotificationBellViewComponent : ViewComponent
    {
        private readonly INotificationService _notificationService;

        public NotificationBellViewComponent(INotificationService notificationService)
        {
            _notificationService = notificationService;
        }

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var userId = HttpContext.User.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Content("");
            }

            var notifications = await _notificationService.GetRecentAsync(userId, 10);
            var unreadCount = await _notificationService.GetUnreadCountAsync(userId);

            ViewBag.UnreadCount = unreadCount;
            return View(notifications);
        }
    }
}
