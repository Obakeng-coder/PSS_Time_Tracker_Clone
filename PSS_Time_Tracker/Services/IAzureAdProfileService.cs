namespace PSS_Time_Tracker.Services
{
    public class AzureAdProfile
    {
        public string? JobTitle { get; set; }
        public string? Department { get; set; }
    }

    /// <summary>
    /// Reads Job Title and Department from the employee's real Azure AD (Entra ID) profile via
    /// Microsoft Graph - closer to how the original app worked, before this local-auth copy stood in
    /// local UserAccount fields for it. Reuses the same app registration/token as the SharePoint
    /// services (see docs/SharePointIntegration.md) but needs an additional Graph permission,
    /// User.Read.All.
    /// </summary>
    public interface IAzureAdProfileService
    {
        /// <summary>Looked up by User Principal Name (usually the same as the employee's email). Null if not found/not configured.</summary>
        Task<AzureAdProfile?> GetProfileAsync(string userPrincipalName);
    }
}
