using System.ComponentModel.DataAnnotations;

namespace PSS_Time_Tracker.Models
{
    public enum NotificationType
    {
        LeaveRequestSubmitted,
        LeaveRequestDecision,
        TimesheetGapFlagged,
        GapResolutionPendingConfirmation
    }

    /// <summary>
    /// One in-app notification for one user - the bell icon in _Layout.cshtml
    /// (see Views/Shared/Components/NotificationBell) reads these for the current user. Created
    /// alongside (not instead of) the existing email notifications in EmailService - this is the
    /// "manager and HR get an app notification" channel, email is the other one.
    /// </summary>
    public class Notification
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string RecipientUserId { get; set; } = "";

        [Required]
        public NotificationType Type { get; set; }

        [Required]
        public string Title { get; set; } = "";

        public string? Message { get; set; }

        /// <summary>Relative app URL to send the user to when they click this notification.</summary>
        public string? Url { get; set; }

        public bool IsRead { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
