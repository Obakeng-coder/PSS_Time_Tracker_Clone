using System.Text.Json;

namespace PSS_Time_Tracker.Services
{
    /// <summary>
    /// Microsoft Graph-backed implementation of <see cref="ISharePointCheckInService"/>, reading the
    /// "MobileCheckIn" list. See docs/SharePointIntegration.md for the full setup (app registration,
    /// permissions, and how to adjust the field-name mapping below if your list's internal column
    /// names differ from the defaults - SharePoint's internal names often aren't what's shown on
    /// screen, e.g. spaces become "_x0020_").
    /// </summary>
    public class SharePointCheckInService : MicrosoftGraphServiceBase, ISharePointCheckInService
    {
        private readonly ILogger<SharePointCheckInService> _logger;

        public SharePointCheckInService(HttpClient httpClient, IConfiguration configuration, ILogger<SharePointCheckInService> logger)
            : base(httpClient, configuration)
        {
            _logger = logger;
        }

        public async Task<SharePointCheckInRecord?> GetCheckInForDateAsync(string employeeFullName, DateTime date)
        {
            if (!IsConfigured())
            {
                return null;
            }

            try
            {
                var listId = await ResolveListIdAsync("ListName", "MobileCheckIn");
                var fieldTitle = Field("Title", "Title");
                var fieldCheckIn = Field("CheckIn", "CheckIn");
                var fieldCheckOut = Field("CheckOut", "CheckOut");
                var fieldWorkLocation = Field("WorkLocation", "WorkLocation");
                var fieldReportsTo = Field("ReportsTo", "ReportsTo");

                var dayStart = date.Date;

                // Matched by full name against Title - the list has no dedicated employee-email column.
                var url = $"{GraphBaseUrl}/sites/{await ResolveSiteIdAsync()}/lists/{listId}/items" +
                          $"?expand=fields&$top=1" +
                          $"&$filter=fields/{fieldTitle} eq '{Uri.EscapeDataString(employeeFullName)}'" +
                          $" and fields/Date eq '{dayStart:yyyy-MM-dd}'";

                using var request = new HttpRequestMessage(HttpMethod.Get, url);
                // SharePoint list filtering via Graph needs this header unless the filtered columns are
                // indexed - fine for a list this size, but ask IT to index Title/Date if the list grows
                // large and this starts timing out.
                request.Headers.Add("Prefer", "HonorNonIndexedQueriesWarningMayFailRandomly");

                var response = await SendAuthenticatedAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("SharePoint check-in lookup failed: {Status}", response.StatusCode);
                    return null;
                }

                using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                var items = doc.RootElement.GetProperty("value");
                if (items.GetArrayLength() == 0)
                {
                    return null;
                }

                var fields = items[0].GetProperty("fields");

                // "Reports to" is stored as free text "Manager Name | Company" (e.g.
                // "Peter Bereta | Providence Software ZA"), not an email - keep only the name portion.
                var reportsToRaw = GetString(fields, fieldReportsTo);
                var reportsToName = reportsToRaw?.Split('|')[0].Trim();

                return new SharePointCheckInRecord
                {
                    EmployeeName = GetString(fields, fieldTitle) ?? "",
                    CheckInTime = CombineDateAndTime(date, GetString(fields, fieldCheckIn)),
                    CheckOutTime = CombineDateAndTime(date, GetString(fields, fieldCheckOut)),
                    WorkLocation = GetString(fields, fieldWorkLocation),
                    ReportsToName = string.IsNullOrWhiteSpace(reportsToName) ? null : reportsToName
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading SharePoint MobileCheckIn list for {Name} on {Date}", employeeFullName, date);
                return null;
            }
        }

        // CheckIn/CheckOut are stored as plain time-of-day text (e.g. "08:03"), not full datetimes -
        // combined here with the row's own Date.
        private static DateTime? CombineDateAndTime(DateTime date, string? timeText)
        {
            if (string.IsNullOrWhiteSpace(timeText))
            {
                return null;
            }

            return TimeSpan.TryParse(timeText, out var timeOfDay) ? date.Date + timeOfDay : null;
        }

    }
}
