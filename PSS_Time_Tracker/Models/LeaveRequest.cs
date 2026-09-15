using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PSS_Time_Tracker.Models
{
    /// <summary>
    /// One row per leave request. Field names/shape follow "Leave Form Template-Final March 2025 1.pdf"
    /// (the real Providence Managed Services employee leave application) field-for-field:
    ///   - Employee section        -> AzureAdUserId, AddressDuringLeave, TelephoneNumber (+ UserAccount profile fields)
    ///   - Leave Request section   -> LeaveTypeId/OtherLeaveDescription, StartDate, EndDate, TotalDays
    ///   - Signature of Requestor  -> EmployeeSignature / EmployeeSignatureDate
    ///   - Recommendation by Reporting Manager -> Manager* columns
    ///   - Approved by HR Manager  -> Hr* columns
    ///   - Captured By / Verified By / Administration-Payroll -> Captured*/Verified* columns
    /// </summary>
    public class LeaveRequest
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string AzureAdUserId { get; set; } = "";

        [Required]
        public int LeaveTypeId { get; set; }

        [ForeignKey(nameof(LeaveTypeId))]
        public LeaveType? LeaveType { get; set; }

        /// <summary>Free-text detail when the employee picks the "Other Leave (please specify)" option.</summary>
        public string? OtherLeaveDescription { get; set; }

        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        /// <summary>Computed server-side from the date range - never trusted from the client.</summary>
        public double TotalDays { get; set; }

        public string? AddressDuringLeave { get; set; }
        public string? TelephoneNumber { get; set; }
        public string? Reason { get; set; }

        public string? EmployeeSignature { get; set; }
        public DateTime? EmployeeSignatureDate { get; set; }

        public LeaveRequestStatus Status { get; set; } = LeaveRequestStatus.PendingManager;

        // --- Stage 1: Reporting Manager ---
        public ManagerRecommendation ManagerRecommendation { get; set; } = ManagerRecommendation.None;
        public bool? ManagerApprovedPaidLeave { get; set; }
        public string? ManagerRemarks { get; set; }
        public string? ManagerSignature { get; set; }
        public DateTime? ManagerDecisionDate { get; set; }
        public string? ManagerUserId { get; set; }

        // --- Stage 2: HR Manager ---
        public HrDecisionType HrDecision { get; set; } = HrDecisionType.None;
        public string? HrRemarks { get; set; }
        public string? HrSignature { get; set; }
        public DateTime? HrDecisionDate { get; set; }
        public string? HrUserId { get; set; }

        // --- Stage 3: Administration / Payroll capture ---
        public string? CapturedByUserId { get; set; }
        public DateTime? CapturedDate { get; set; }
        public string? VerifiedByUserId { get; set; }
        public DateTime? VerifiedDate { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.Now;
    }
}
