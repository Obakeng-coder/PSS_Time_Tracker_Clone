using PSS_Time_Tracker.Models;

namespace PSS_Time_Tracker.Services
{
    /// <summary>The in-app "bell icon" notification channel - separate from and in addition to
    /// EmailService's emails, not a replacement for them. See Views/Shared/Components/NotificationBell.</summary>
    public interface INotificationService
    {
        Task CreateAsync(string recipientUserId, NotificationType type, string title, string? message = null, string? url = null);

        /// <summary>Convenience for notifying several recipients (e.g. every HR user) with the same
        /// content in one go.</summary>
        Task CreateForManyAsync(IEnumerable<string> recipientUserIds, NotificationType type, string title, string? message = null, string? url = null);

        Task<List<Notification>> GetRecentAsync(string userId, int count = 10);
        Task<int> GetUnreadCountAsync(string userId);
        Task MarkReadAsync(int notificationId, string userId);
        Task MarkAllReadAsync(string userId);
    }
}
