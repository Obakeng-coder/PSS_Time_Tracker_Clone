using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TimeSheetRecorder.Models;


namespace PSS_Time_Tracker.Models
{
    public class UserAccount
    {
        [Key]
        [Column("AzureAdUserId")]
        public string AzureAdUserId { get; set; } = string.Empty;

        [Required]
        [Column("Email")]
        public string Email { get; set; } = "unknown@domain.com";

        [Column("EmployeeName")]
        public string EmployeeName { get; set; } = "Unknown";

        [Column("EmployeeSurname")]
        public string EmployeeSurname { get; set; } = "User";

        [Column("JobTitle")]
        public string JobTitle { get; set; } = "Not specified";

        // Added for the Leave Management module - these fields appear on the real Employee Leave
        // Application form but had no home in the schema before. All optional/nullable since existing
        // seeded accounts won't have them.
        [Column("Department")]
        public string? Department { get; set; }

        [Column("IdNumber")]
        public string? IdNumber { get; set; }

        [Column("EmployeeNumber")]
        public string? EmployeeNumber { get; set; }

        [Column("PhoneNumber")]
        public string? PhoneNumber { get; set; }

        [Column("ApprovalStatus")]
        public int ApprovalStatus { get; set; } = 1;
        [Column("SupervisorFullName")]
        public string SupervisorFullName { get; set; }

        [Column("SupervisorEmail")]
        public string SupervisorEmail { get; set; }

        // Replaces the old Azure AD "groups" claim check. True for accounts that
        // should see the Manager Board / Approve Employees / Host Companies screens.
        [Column("IsManager")]
        public bool IsManager { get; set; } = false;

        // Separate from IsManager - a line manager only ever sees their own reports' leave requests
        // (recommend/reject stage); HR sees every request once it reaches the HR/Payroll stage
        // (with-pay/without-pay decision + balance capture), regardless of who their manager is. One
        // person can be both (both flags true) if that fits how a smaller team is actually staffed.
        [Column("IsHr")]
        public bool IsHr { get; set; } = false;

        public virtual ICollection<TimeTrackerModel> TimeEntries { get; set; } = new List<TimeTrackerModel>();
    }
}