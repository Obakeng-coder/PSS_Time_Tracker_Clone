namespace PSS_Time_Tracker.Services
{
    /// <summary>
    /// Realistic stand-in for <see cref="SharePointCheckInService"/>, used when
    /// SharePointIntegration:UseMockData is true (see appsettings.Development.json) - lets the whole
    /// Capture Your Timesheet flow be tested without any live Graph/SharePoint credentials at all.
    /// Every value is clearly prefixed "(Mock)" so it's never mistaken for real data if this gets left
    /// on by accident.
    /// </summary>
    public class MockSharePointCheckInService : ISharePointCheckInService
    {
        private static readonly string[] WorkLocations = { "Head Office", "Work From Home", "Client Site", "PSS 35" };

        public Task<SharePointCheckInRecord?> GetCheckInForDateAsync(string employeeFullName, DateTime date)
        {
            // Only "today" has a check-in, matching how a real employee's punch would only exist for
            // the day they actually checked in - keeps the "no check-in yet" path testable too, just
            // by picking any other date on the Capture Your Timesheet form.
            if (date.Date != DateTime.Today)
            {
                return Task.FromResult<SharePointCheckInRecord?>(null);
            }

            var seed = Math.Abs(employeeFullName.GetHashCode());
            var checkInMinute = seed % 20; // 08:00-08:19
            var location = WorkLocations[seed % WorkLocations.Length];

            return Task.FromResult<SharePointCheckInRecord?>(new SharePointCheckInRecord
            {
                EmployeeName = employeeFullName,
                CheckInTime = date.Date.AddHours(8).AddMinutes(checkInMinute),
                CheckOutTime = date.Date.AddHours(17).AddMinutes(checkInMinute),
                WorkLocation = $"(Mock) {location}",
                ReportsToName = "(Mock) Carol Manager"
            });
        }

    }
}
