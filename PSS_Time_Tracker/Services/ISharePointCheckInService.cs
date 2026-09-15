namespace PSS_Time_Tracker.Services
{
    /// <summary>
    /// One employee's row from the SharePoint "MobileCheckIn" list for a given date - see
    /// docs/SharePointIntegration.md for the Graph API contract and exact column mapping.
    /// </summary>
    public class SharePointCheckInRecord
    {
        /// <summary>From the list's "Title" column.</summary>
        public string EmployeeName { get; set; } = "";

        public DateTime? CheckInTime { get; set; }
        public DateTime? CheckOutTime { get; set; }

        /// <summary>From the list's "Work Location" column - replaces the old Host Company concept.</summary>
        public string? WorkLocation { get; set; }

        /// <summary>
        /// Parsed from the list's "Reports to" column, which stores "Manager Name | Company" as text
        /// (not an email) - e.g. "Peter Bereta | Providence Software ZA". Only the name portion is kept.
        /// </summary>
        public string? ReportsToName { get; set; }
    }

    /// <summary>
    /// Reads live check-in/check-out data from the SharePoint "MobileCheckIn" list via Microsoft Graph,
    /// so employees no longer type Employee Name, Start Time, End Time, or Work Location by hand -
    /// every one of those is pulled read-only from whatever the mobile check-in app already recorded.
    /// Only the date remains user-editable on the Capture Your Timesheet screen.
    /// </summary>
    public interface ISharePointCheckInService
    {
        /// <summary>
        /// The signed-in employee's check-in record for a specific date, or null if none exists yet.
        /// Matched by full name against the list's "Title" column - there's no dedicated email column.
        /// </summary>
        Task<SharePointCheckInRecord?> GetCheckInForDateAsync(string employeeFullName, DateTime date);
    }
}
