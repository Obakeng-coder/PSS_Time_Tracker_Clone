using System.Text.Json;

namespace PSS_Time_Tracker.Services
{
    /// <summary>
    /// Microsoft Graph-backed implementation of <see cref="ISharePointLeaveBalanceService"/>, reading
    /// the "LeaveInformation" list. See docs/SharePointIntegration.md.
    /// </summary>
    public class SharePointLeaveBalanceService : MicrosoftGraphServiceBase, ISharePointLeaveBalanceService
    {
        private readonly ILogger<SharePointLeaveBalanceService> _logger;

        public SharePointLeaveBalanceService(HttpClient httpClient, IConfiguration configuration, ILogger<SharePointLeaveBalanceService> logger)
            : base(httpClient, configuration)
        {
            _logger = logger;
        }

        public async Task<SharePointLeaveBalanceRecord?> GetLeaveBalanceAsync(string employeeEmail)
        {
            if (!IsConfigured() || string.IsNullOrWhiteSpace(employeeEmail))
            {
                return null;
            }

            try
            {
                var listId = await ResolveListIdAsync("LeaveListName", "LeaveInformation");
                var fieldEmail = Field("LeaveEmail", "Title");
                var fieldDisplayName = Field("LeaveDisplayName", "displayName");
                var fieldAnnualLeave = Field("AnnualLeave", "AnnualLeave");
                var fieldAnnualLeavesUsed = Field("AnnualLeavesUsed", "AnnualLeavesUsed");
                var fieldSickLeave = Field("SickLeave", "SickLeave");
                var fieldSicksLeavesUsed = Field("SicksLeavesUsed", "SicksLeavesUsed");
                var fieldFamilyResp = Field("FamilyResponsibilityLeave", "FamilyResponsibilityLeave");
                var fieldFamilyRespUsed = Field("FamilyResponsibilityLeaveUsed", "FamilyResponsibilityLeaveUsed");
                var fieldUnpaid = Field("UnpaidLeaves", "UnpaidLeaves");
                var fieldMaternity = Field("MaternityLeave", "MaternityLeave");
                var fieldPaternity = Field("PaternityLeave", "PaternityLeave");

                // Matched by email against "Title" (not name/displayName) - email is a reliable key,
                // and Graph's list-item $filter doesn't support a case-insensitive "eq" (no tolower()
                // support here), so every row is pulled back and compared case-insensitively in memory
                // instead of risking a real match being missed over a casing difference.
                var url = $"{GraphBaseUrl}/sites/{await ResolveSiteIdAsync()}/lists/{listId}/items" +
                          $"?expand=fields&$top=999";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                request.Headers.Add("Prefer", "HonorNonIndexedQueriesWarningMayFailRandomly");

                var response = await SendAuthenticatedAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("SharePoint leave balance lookup failed: {Status}", response.StatusCode);
                    return null;
                }

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var items = doc.RootElement.GetProperty("value");

                JsonElement fields = default;
                var found = false;
                foreach (var item in items.EnumerateArray())
                {
                    var candidateFields = item.GetProperty("fields");
                    var candidateEmail = GetString(candidateFields, fieldEmail);
                    if (!string.IsNullOrWhiteSpace(candidateEmail) &&
                        string.Equals(candidateEmail.Trim(), employeeEmail.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        fields = candidateFields;
                        found = true;
                        break;
                    }
                }

                if (!found)
                {
                    _logger.LogInformation("No SharePoint LeaveInformation row matched {Email}", employeeEmail);
                    return null;
                }

                var displayNameRaw = GetString(fields, fieldDisplayName);

                return new SharePointLeaveBalanceRecord
                {
                    DisplayName = displayNameRaw?.Split('|')[0].Trim(),
                    AnnualLeave = GetNumber(fields, fieldAnnualLeave),
                    AnnualLeavesUsed = GetNumber(fields, fieldAnnualLeavesUsed),
                    SickLeave = GetNumber(fields, fieldSickLeave),
                    SicksLeavesUsed = GetNumber(fields, fieldSicksLeavesUsed),
                    FamilyResponsibilityLeave = GetNumber(fields, fieldFamilyResp),
                    FamilyResponsibilityLeaveUsed = GetNumber(fields, fieldFamilyRespUsed),
                    UnpaidLeaves = GetNumber(fields, fieldUnpaid),
                    MaternityLeave = GetString(fields, fieldMaternity),
                    PaternityLeave = GetString(fields, fieldPaternity)
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading SharePoint LeaveInformation list for {Email}", employeeEmail);
                return null;
            }
        }
    }
}
