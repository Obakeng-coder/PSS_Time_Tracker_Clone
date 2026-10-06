using Microsoft.EntityFrameworkCore;
using PSS_Time_Tracker.Data;
using PSS_Time_Tracker.Models;

namespace PSS_Time_Tracker.Services
{
    /// <inheritdoc cref="INotificationService"/>
    public class NotificationService : INotificationService
    {
        private readonly timeSheetRecorderContext _context;
        private readonly ITeamsNotificationService _teams;
        private readonly EmailService _email;
        private readonly IConfiguration _configuration;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            timeSheetRecorderContext context, ITeamsNotificationService teams, EmailService email,
            IConfiguration configuration, ILogger<NotificationService> logger)
        {
            _context = context;
            _teams = teams;
            _email = email;
            _configuration = configuration;
            _logger = logger;
        }

        // Every bell notification also goes out by email and to the recipient's Teams chat, so there's
        // one place new notifications get created and one place they fan out. Both are best-effort: a
        // mail or Teams failure is logged and never blocks the action that raised the notification.
        private async Task PushToTeamsAsync(IEnumerable<string> recipientUserIds, string title, string? message, string? url)
        {
            var ids = recipientUserIds.ToList();
            var recipients = await _context.Users
                .Where(u => ids.Contains(u.AzureAdUserId) && u.Email != null)
                .Select(u => new { u.Email, u.EmployeeName })
                .ToListAsync();

            var baseUrl = (_configuration["Teams:AppBaseUrl"] ?? "").TrimEnd('/');
            var link = !string.IsNullOrWhiteSpace(url) && url.StartsWith('/') && baseUrl.Length > 0 ? baseUrl + url : null;

            foreach (var r in recipients)
            {
                try
                {
                    var body = $"Hi {r.EmployeeName},\n\n{title}\n" +
                        (string.IsNullOrWhiteSpace(message) ? "" : $"\n{message}\n") +
                        (link == null ? "" : $"\nOpen HourTrack: {link}\n") +
                        "\nRegards,\nHourTrack Team";
                    await _email.SendAsync(r.Email, title, body);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Couldn't email the notification to {Email}", r.Email);
                }

                await _teams.SendAsync(r.Email, title, message, url);
            }
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
            await PushToTeamsAsync(new[] { recipientUserId }, title, message, url);
        }

        public async Task CreateForManyAsync(IEnumerable<string> recipientUserIds, NotificationType type, string title, string? message = null, string? url = null)
        {
            var recipients = recipientUserIds.Where(id => !string.IsNullOrWhiteSpace(id)).Distinct().ToList();
            foreach (var recipientUserId in recipients)
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
            await PushToTeamsAsync(recipients, title, message, url);
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
