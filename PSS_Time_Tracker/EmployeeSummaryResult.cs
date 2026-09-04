namespace PSS_Time_Tracker
{
    public class EmployeeSummaryResult
    {
        public string AzureAdUserId { get; set; }
        public string EmployeeName { get; set; }
        public string EmployeeSurname { get; set; }
        public int TimesheetCount { get; set; }
        public DateTime LatestEntry { get; set; }
    }

}
