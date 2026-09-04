namespace PSS_Time_Tracker.Models
{
    public class EmployeeViewModel
    {
        public string AzureAdUserId { get; set; }
        public string FullName { get; set; }
        public int TimesheetCount { get; set; }
        public DateTime LatestEntry { get; set; }
    }
}