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

        // How far back a mock punch exists - real SharePointCheckInService looks up whatever date it's
        // asked for (the MobileCheckIn list keeps every past row, not just today's), so a genuine
        // employee backfilling a missed day still finds their real punch from that day. The mock only
        // ever "remembers" a punch for today by default; this window lets local testing of that
        // backfill path (TimeTrackerController.ValidateAgainstUnresolvedGapsAsync) exercise the same
        // "found a real check-in for an earlier date" case without needing live Graph credentials.
        private const int MockPunchLookbackDays = 30;

        public Task<SharePointCheckInRecord?> GetCheckInForDateAsync(string employeeFullName, DateTime date)
        {
            // No punch in the future, and none further back than the window above - keeps the
            // "no check-in yet" path testable too, just by picking a date outside this range.
            if (date.Date > DateTime.Today || date.Date < DateTime.Today.AddDays(-MockPunchLookbackDays))
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
