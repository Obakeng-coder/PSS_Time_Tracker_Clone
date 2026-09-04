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

        public virtual ICollection<TimeTrackerModel> TimeEntries { get; set; } = new List<TimeTrackerModel>();
    }
}