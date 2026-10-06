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

        public async Task<AzureAdProfile?> GetProfileAsync(string email)
        {
            if (!IsConfigured() || string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            try
            {
                // GET /users/{key} only accepts an object id or an EXACT userPrincipalName - not an
                // arbitrary mail/SMTP address, so a direct path lookup 404s for any real employee whose
                // UPN differs from their email (a second verified domain, an alias, an on-prem AD
                // migration). A $filter query checks both properties instead - same fix already applied
                // to AzureAdProvisioningService's directory lookup.
                var escapedEmail = email.Replace("'", "''");
                var url = $"{GraphBaseUrl}/users?$filter=mail eq '{Uri.EscapeDataString(escapedEmail)}' " +
                    $"or userPrincipalName eq '{Uri.EscapeDataString(escapedEmail)}'" +
                    "&$select=jobTitle,department";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);

                var response = await SendAuthenticatedAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    // Not necessarily an error worth alarming about - e.g. the local seeded test
                    // accounts use @local.test addresses that don't resolve to any real Azure AD user.
                    _logger.LogWarning("Azure AD profile lookup for {Email} failed: {Status}", email, response.StatusCode);
                    return null;
                }

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var matches = doc.RootElement.GetProperty("value");
                if (matches.GetArrayLength() == 0)
                {
                    return null;
                }

                var root = matches[0];
                return new AzureAdProfile
                {
                    JobTitle = GetStringProperty(root, "jobTitle"),
                    Department = GetStringProperty(root, "department")
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading Azure AD profile for {Email}", email);
                return null;
            }
        }

        private static string? GetStringProperty(JsonElement element, string propertyName) =>
            element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;
    }
}
