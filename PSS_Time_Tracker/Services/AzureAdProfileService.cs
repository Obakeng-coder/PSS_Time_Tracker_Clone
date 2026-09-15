using System.Text.Json;

namespace PSS_Time_Tracker.Services
{
    /// <summary>
    /// Microsoft Graph-backed implementation of <see cref="IAzureAdProfileService"/>.
    /// See docs/SharePointIntegration.md for the extra permission this needs (User.Read.All) on top
    /// of the app registration already set up for the SharePoint services.
    /// </summary>
    public class AzureAdProfileService : MicrosoftGraphServiceBase, IAzureAdProfileService
    {
        private readonly ILogger<AzureAdProfileService> _logger;

        public AzureAdProfileService(HttpClient httpClient, IConfiguration configuration, ILogger<AzureAdProfileService> logger)
            : base(httpClient, configuration)
        {
            _logger = logger;
        }

        public async Task<AzureAdProfile?> GetProfileAsync(string userPrincipalName)
        {
            if (!IsConfigured())
            {
                return null;
            }

            try
            {
                var url = $"{GraphBaseUrl}/users/{Uri.EscapeDataString(userPrincipalName)}?$select=jobTitle,department";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);

                var response = await SendAuthenticatedAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    // A 404 here just means this employee's TymSheet email doesn't match a real Azure
                    // AD account yet (e.g. the local seeded test accounts use @local.test addresses) -
                    // not necessarily an error worth alarming about, so this stays at Warning.
                    _logger.LogWarning("Azure AD profile lookup for {Upn} failed: {Status}", userPrincipalName, response.StatusCode);
                    return null;
                }

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                return new AzureAdProfile
                {
                    JobTitle = GetStringProperty(doc.RootElement, "jobTitle"),
                    Department = GetStringProperty(doc.RootElement, "department")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading Azure AD profile for {Upn}", userPrincipalName);
                return null;
            }
        }

        private static string? GetStringProperty(JsonElement element, string propertyName) =>
            element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }
}
