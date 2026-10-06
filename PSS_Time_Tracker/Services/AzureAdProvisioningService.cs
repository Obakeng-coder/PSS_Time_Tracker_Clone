using System.Net.Http.Headers;
using System.Text.Json;
using PSS_Time_Tracker.Models;

namespace PSS_Time_Tracker.Services
{
    /// <inheritdoc cref="IAzureAdProvisioningService"/>
    public class AzureAdProvisioningService : MicrosoftGraphServiceBase, IAzureAdProvisioningService
    {
        private readonly ILogger<AzureAdProvisioningService> _logger;

        public AzureAdProvisioningService(HttpClient httpClient, IConfiguration configuration, ILogger<AzureAdProvisioningService> logger)
            : base(httpClient, configuration)
        {
            _logger = logger;
        }

        // Reuses the App Registration already configured for interactive sign-in (AzureAd:*) rather
        // than requiring SharePointIntegration to be set up too just to look someone up in the
        // directory - see MicrosoftGraphServiceBase.ConfigSectionName.
        protected override string ConfigSectionName => "AzureAd";

        public async Task<UserAccount?> TryBuildProfileAsync(string email)
        {
            if (string.IsNullOrWhiteSpace(email))
            {
                return null;
            }

            // Logged loudly and distinctly from "Graph genuinely doesn't know this person" below -
            // this means AzureAd:ClientSecret (or TenantId/ClientId) isn't set at all, so nothing was
            // actually asked of Graph. Left as a plain silent null before, this was indistinguishable
            // from a real "not in the directory" result to anyone reading the sign-in refusal message,
            // which is actively misleading while someone's still finishing setup.
            if (!IsConfigured())
            {
                _logger.LogWarning(
                    "Skipped the Azure AD directory lookup for {Email} - AzureAd:ClientSecret (or TenantId/ClientId) " +
                    "isn't configured, so this isn't a real 'not found in the directory' result.", email);
                return null;
            }

            try
            {
                // GET /users/{key} only accepts an object id or an EXACT userPrincipalName - not an
                // arbitrary mail/SMTP address. Plenty of real tenants have a UPN that differs from the
                // employee's actual email (a second verified domain, an on-prem AD migration, an alias)
                // - a direct path lookup on "mail" would 404 for a genuinely real employee in exactly
                // that case. A $filter query checks both properties instead, so either one matching what
                // Azure AD's own OIDC token claimed as this person's email finds them correctly.
                var escapedEmail = email.Replace("'", "''"); // OData literal escaping, not URL escaping
                var url = $"{GraphBaseUrl}/users?$filter=mail eq '{Uri.EscapeDataString(escapedEmail)}' " +
                    $"or userPrincipalName eq '{Uri.EscapeDataString(escapedEmail)}'" +
                    "&$select=id,mail,userPrincipalName,givenName,surname,displayName,jobTitle,department";
                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                var response = await SendAuthenticatedAsync(request);

                if (!response.IsSuccessStatusCode)
                {
                    // Graph returns 200 with an empty "value" array for "no match" on a filter query
                    // (unlike the 404 a direct /users/{key} lookup would give) - a non-success status
                    // here is always something worth knowing about (403, 5xx, a malformed filter, ...),
                    // never the normal "not a real employee" case.
                    _logger.LogWarning("Azure AD directory lookup for {Email} failed: {Status}", email, response.StatusCode);
                    return null;
                }

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var matches = doc.RootElement.GetProperty("value");
                if (matches.GetArrayLength() == 0)
                {
                    // Genuinely not a real user in this tenant - the normal "someone typo'd their email"
                    // or "random internet noise hitting the login page" case.
                    return null;
                }

                var root = matches[0];

                var oid = GetString(root, "id");
                if (string.IsNullOrWhiteSpace(oid))
                {
                    return null;
                }

                var givenName = GetString(root, "givenName");
                var surname = GetString(root, "surname");
                // Falls back to splitting displayName only for the rare directory entry with no
                // givenName/surname set - TimeTrackerController.SplitFullName does the same for
                // SharePoint's single "Title" string, but Graph gives us the two fields directly here,
                // which is the more reliable source when it's actually populated.
                if (string.IsNullOrWhiteSpace(givenName) && string.IsNullOrWhiteSpace(surname))
                {
                    var displayName = GetString(root, "displayName") ?? email;
                    var parts = displayName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
                    givenName = parts.Length > 1 ? string.Join(' ', parts[..^1]) : displayName;
                    surname = parts.Length > 1 ? parts[^1] : "";
                }

                var account = new UserAccount
                {
                    AzureAdUserId = oid,
                    Email = GetString(root, "mail") ?? GetString(root, "userPrincipalName") ?? email,
                    EmployeeName = givenName ?? "",
                    EmployeeSurname = surname ?? "",
                    JobTitle = GetString(root, "jobTitle") ?? "Not specified",
                    Department = GetString(root, "department"),
                    ApprovalStatus = 1,
                    IsManager = false,
                    IsHr = false,
                    // Overwritten below if the manager lookup succeeds; "." is this app's established
                    // placeholder for "no supervisor on file" (see TimeTrackerController's own fallback).
                    SupervisorFullName = ".",
                    SupervisorEmail = "."
                };

                await TryFillManagerAsync(account, oid);

                return account;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error looking up {Email} in the Azure AD directory", email);
                return null;
            }
        }

        // Best-effort: a missing/failed manager lookup (no manager set in the directory, insufficient
        // permission, transient error) just leaves the "." placeholder rather than failing provisioning
        // over a field the rest of the app already treats as optional.
        private async Task TryFillManagerAsync(UserAccount account, string oid)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get,
                    $"{GraphBaseUrl}/users/{oid}/manager?$select=displayName,mail,userPrincipalName");
                var response = await SendAuthenticatedAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    return;
                }

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var managerName = GetString(doc.RootElement, "displayName");
                var managerEmail = GetString(doc.RootElement, "mail") ?? GetString(doc.RootElement, "userPrincipalName");

                if (!string.IsNullOrWhiteSpace(managerName))
                {
                    account.SupervisorFullName = managerName;
                }
                if (!string.IsNullOrWhiteSpace(managerEmail))
                {
                    account.SupervisorEmail = managerEmail;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Couldn't read the manager for {Oid} - leaving it unset", oid);
            }
        }
    }
}
