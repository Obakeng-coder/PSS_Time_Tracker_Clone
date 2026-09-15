using Microsoft.EntityFrameworkCore;
using PSS_Time_Tracker.Data;
using PSS_Time_Tracker.Models;

namespace PSS_Time_Tracker.Services
{
    /// <inheritdoc cref="INotificationService"/>
    public class NotificationService : INotificationService
    {
        private readonly timeSheetRecorderContext _context;

        public NotificationService(timeSheetRecorderContext context)
        {
            _context = context;
        }

        public async Task CreateAsync(string recipientUserId, NotificationType type, string title, string? message = null, string? url = null)
        {
            if (string.IsNullOrWhiteSpace(recipientUserId))
            {
                return;
            }

            _context.Notifications.Add(new Notification
            {
                RecipientUserId = recipientUserId,
                Type = type,
                Title = title,
                Message = message,
                Url = url
            });
            await _context.SaveChangesAsync();
        }

        public async Task CreateForManyAsync(IEnumerable<string> recipientUserIds, NotificationType type, string title, string? message = null, string? url = null)
        {
            foreach (var recipientUserId in recipientUserIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct())
            {
                _context.Notifications.Add(new Notification
                {
                    RecipientUserId = recipientUserId,
                    Type = type,
                    Title = title,
                    Message = message,
                    Url = url
                });
            }
            await _context.SaveChangesAsync();
        }

        public async Task<List<Notification>> GetRecentAsync(string userId, int count = 10)
        {
            return await _context.Notifications
                .Where(n => n.RecipientUserId == userId)
                .OrderByDescending(n => n.CreatedDate)
                .Take(count)
                .ToListAsync();
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            return await _context.Notifications.CountAsync(n => n.RecipientUserId == userId && !n.IsRead);
        }

        public async Task MarkReadAsync(int notificationId, string userId)
        {
            var notification = await _context.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.RecipientUserId == userId);
            if (notification != null && !notification.IsRead)
            {
                notification.IsRead = true;
                await _context.SaveChangesAsync();
            }
        }

        public async Task MarkAllReadAsync(string userId)
        {
            await _context.Notifications
                .Where(n => n.RecipientUserId == userId && !n.IsRead)
                .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true));
        }
    }
}
