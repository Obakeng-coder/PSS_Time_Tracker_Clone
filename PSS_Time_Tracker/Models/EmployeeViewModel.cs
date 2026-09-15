namespace PSS_Time_Tracker.Models
{
    public class EmployeeViewModel
    {
        public string AzureAdUserId { get; set; }
        public string FullName { get; set; }
        public int TimesheetCount { get; set; }
        // Nullable: an employee with zero TimeTracker rows (still shown on the Manager Board since
        // Manager/Index's stored procs now LEFT JOIN rather than requiring at least one row) has no
        // "latest entry".
        public DateTime? LatestEntry { get; set; }
    }
}