using System.ComponentModel.DataAnnotations;

namespace PSS_Time_Tracker.Models
{
    /// <summary>View model for the "Request Time Off" form (LeaveController.Create).</summary>
    public class LeaveRequestViewModel
    {
        public string AzureAdUserId { get; set; } = "";

        // Read-only display fields, prefilled from UserAccount - same pattern as TimeTrackerViewModel.
        public string EmployeeName { get; set; } = "";
        public string EmployeeSurname { get; set; } = "";

        // Nothing in Azure AD/SharePoint reliably supplies these (Department sometimes does via Graph,
        // ID/Employee Number never do), so unlike the fields above these are editable - prefilled from
        // UserAccount when already on file, saved back there on submit so the employee only has to type
        // them once.
        [Required(ErrorMessage = "Job Title is required.")]
        [StringLength(100)]
        public string? JobTitle { get; set; }

        [Required(ErrorMessage = "Department is required.")]
        public string? Department { get; set; }

        [Required(ErrorMessage = "ID Number is required.")]
        public string? IdNumber { get; set; }

        public string? EmployeeNumber { get; set; }

        [Required(ErrorMessage = "Please select a leave type.")]
        public int LeaveTypeId { get; set; }

        public List<LeaveType> LeaveTypeOptions { get; set; } = new();

        /// <summary>Only required when the selected leave type is "Other".</summary>
        public string? OtherLeaveDescription { get; set; }

        [Required(ErrorMessage = "Start Date is required.")]
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [Required(ErrorMessage = "End Date is required.")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        public string? AddressDuringLeave { get; set; }

        [Phone]
        public string? TelephoneNumber { get; set; }

        [MaxLength(500)]
        public string? Reason { get; set; }

        [Required(ErrorMessage = "Please sign the form.")]
        public string EmployeeSignature { get; set; } = "";

        // Populated after a balance check for display; not posted back.
        public double? RemainingBalance { get; set; }
    }
}
