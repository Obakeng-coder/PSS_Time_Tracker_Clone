namespace PSS_Time_Tracker.Services
{
    /// <summary>
    /// Pushes a notification to a person's Microsoft Teams chat, alongside (not instead of) the in-app
    /// bell and the email. Delivery goes through a Power Automate flow (Teams:FlowUrl) that posts as the
    /// Flow bot to the recipient - see TeamsNotificationService. Never throws: a Teams outage or an
    /// unconfigured flow must not break the action that triggered the notification.
    /// </summary>
    public interface ITeamsNotificationService
    {
        Task SendAsync(string recipientEmail, string title, string? message, string? relativeUrl);
    }
}
