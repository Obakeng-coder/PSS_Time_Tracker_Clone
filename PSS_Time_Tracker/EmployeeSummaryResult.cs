namespace PSS_Time_Tracker
{
    public class EmployeeSummaryResult
    {
        public string AzureAdUserId { get; set; }
        public string EmployeeName { get; set; }
        public string EmployeeSurname { get; set; }
        public int TimesheetCount { get; set; }
        // Nullable: GetManagerEmployeeTimeSheetsPaged.sql now LEFT JOINs TimeTracker so every managed
        // employee is included, not just ones with at least one row - MAX(t.DateOfEntry) is NULL for
        // an employee with zero entries.
        public DateTime? LatestEntry { get; set; }
    }

}
