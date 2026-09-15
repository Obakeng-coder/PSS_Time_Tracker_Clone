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

        public async Task<SharePointLeaveBalanceRecord?> GetLeaveBalanceAsync(string employeeFullName)
        {
            if (!IsConfigured())
            {
                return null;
            }

            try
            {
                var listId = await ResolveListIdAsync("LeaveListName", "LeaveInformation");
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

                // displayName stores "Name | Company" - startswith rather than eq, so the exact
                // company suffix formatting doesn't have to match.
                var url = $"{GraphBaseUrl}/sites/{await ResolveSiteIdAsync()}/lists/{listId}/items" +
                          $"?expand=fields&$top=1" +
                          $"&$filter=startswith(fields/{fieldDisplayName},'{Uri.EscapeDataString(employeeFullName)}')";

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
                if (items.GetArrayLength() == 0)
                {
                    return null;
                }

                var fields = items[0].GetProperty("fields");
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
                _logger.LogError(ex, "Error reading SharePoint LeaveInformation list for {Name}", employeeFullName);
                return null;
            }
        }
    }
}
