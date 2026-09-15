using System.Net.Http.Headers;
using System.Text.Json;

namespace PSS_Time_Tracker.Services
{
    /// <summary>
    /// Shared Microsoft Graph plumbing (app-only OAuth token, plus SharePoint site/list ID resolution)
    /// for every Graph-backed service in the app - <see cref="SharePointCheckInService"/> (MobileCheckIn
    /// list), <see cref="SharePointLeaveBalanceService"/> (LeaveInformation list), and
    /// <see cref="AzureAdProfileService"/> (Job Title/Department from the employee's Azure AD profile,
    /// which doesn't touch SharePoint at all but reuses the same token acquisition). Kept in one place
    /// since the auth handling is the most security-sensitive part and shouldn't duplicate/drift across
    /// services. See docs/SharePointIntegration.md.
    /// </summary>
    public abstract class MicrosoftGraphServiceBase
    {
        protected const string GraphBaseUrl = "https://graph.microsoft.com/v1.0";

        private readonly HttpClient _httpClient;
        protected readonly IConfiguration Configuration;

        private string? _cachedToken;
        private DateTimeOffset _tokenExpiresAt = DateTimeOffset.MinValue;
        private string? _cachedSiteId;
        private readonly Dictionary<string, string> _cachedListIds = new();

        protected MicrosoftGraphServiceBase(HttpClient httpClient, IConfiguration configuration)
        {
            _httpClient = httpClient;
            Configuration = configuration;
        }

        protected bool IsConfigured() =>
            !string.IsNullOrEmpty(Configuration["SharePointIntegration:TenantId"]) &&
            !string.IsNullOrEmpty(Configuration["SharePointIntegration:ClientId"]) &&
            !string.IsNullOrEmpty(Configuration["SharePointIntegration:ClientSecret"]);

        protected string Field(string configSuffix, string fallback) =>
            Configuration[$"SharePointIntegration:Fields:{configSuffix}"] ?? fallback;

        protected async Task<string> ResolveSiteIdAsync()
        {
            if (_cachedSiteId != null)
            {
                return _cachedSiteId;
            }

            var hostname = Configuration["SharePointIntegration:SiteHostname"] ?? "providencesoft.sharepoint.com";
            var sitePath = Configuration["SharePointIntegration:SitePath"] ?? "/sites/ProvidenceInternal";

            using var request = new HttpRequestMessage(HttpMethod.Get, $"{GraphBaseUrl}/sites/{hostname}:{sitePath}");
            var response = await SendAuthenticatedAsync(request);
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            _cachedSiteId = doc.RootElement.GetProperty("id").GetString();
            return _cachedSiteId!;
        }

        /// <param name="listNameConfigKey">Config key under SharePointIntegration for this list's name, e.g. "ListName" or "LeaveListName".</param>
        protected async Task<string> ResolveListIdAsync(string listNameConfigKey, string defaultListName)
        {
            var listName = Configuration[$"SharePointIntegration:{listNameConfigKey}"] ?? defaultListName;

            if (_cachedListIds.TryGetValue(listName, out var cached))
            {
                return cached;
            }

            var siteId = await ResolveSiteIdAsync();

            using var request = new HttpRequestMessage(HttpMethod.Get,
                $"{GraphBaseUrl}/sites/{siteId}/lists?$filter=displayName eq '{Uri.EscapeDataString(listName)}'");
            var response = await SendAuthenticatedAsync(request);
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var lists = doc.RootElement.GetProperty("value");
            if (lists.GetArrayLength() == 0)
            {
                throw new InvalidOperationException($"SharePoint list '{listName}' not found at the configured site.");
            }

            var listId = lists[0].GetProperty("id").GetString()!;
            _cachedListIds[listName] = listId;
            return listId;
        }

        protected async Task<HttpResponseMessage> SendAuthenticatedAsync(HttpRequestMessage request)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", await GetAccessTokenAsync());
            return await _httpClient.SendAsync(request);
        }

        private async Task<string> GetAccessTokenAsync()
        {
            if (_cachedToken != null && DateTimeOffset.UtcNow < _tokenExpiresAt)
            {
                return _cachedToken;
            }

            var tenantId = Configuration["SharePointIntegration:TenantId"];
            var clientId = Configuration["SharePointIntegration:ClientId"];
            var clientSecret = Configuration["SharePointIntegration:ClientSecret"];

            var tokenRequest = new HttpRequestMessage(HttpMethod.Post,
                $"https://login.microsoftonline.com/{tenantId}/oauth2/v2.0/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = clientId!,
                    ["client_secret"] = clientSecret!,
                    ["scope"] = "https://graph.microsoft.com/.default",
                    ["grant_type"] = "client_credentials"
                })
            };

            var response = await _httpClient.SendAsync(tokenRequest);
            response.EnsureSuccessStatusCode();

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            _cachedToken = doc.RootElement.GetProperty("access_token").GetString();
            var expiresIn = doc.RootElement.GetProperty("expires_in").GetInt32();
            // Refresh a minute early rather than cutting it exactly at expiry.
            _tokenExpiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn - 60);

            return _cachedToken!;
        }

        protected static string? GetString(JsonElement fields, string fieldName) =>
            fields.TryGetProperty(fieldName, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        protected static double GetNumber(JsonElement fields, string fieldName) =>
            fields.TryGetProperty(fieldName, out var value) &&
            (value.ValueKind == JsonValueKind.Number ||
             (value.ValueKind == JsonValueKind.String && double.TryParse(value.GetString(), out _)))
                ? value.ValueKind == JsonValueKind.Number ? value.GetDouble() : double.Parse(value.GetString()!)
                : 0;
    }
}
