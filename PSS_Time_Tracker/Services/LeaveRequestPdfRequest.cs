using PSS_Time_Tracker.Models;

namespace PSS_Time_Tracker.Services
{
    /// <summary>
    /// Everything needed to render one "Employee Leave Application" PDF (see
    /// docs - the real Providence Managed Services paper form this mirrors field-for-field).
    /// Built by the calling controller from <see cref="LeaveRequest"/> + <see cref="UserAccount"/> (the
    /// employee's Department/IdNumber/EmployeeNumber live on the account, not the request) plus the
    /// Captured/Verified-by names resolved from their user ids, and the live SharePoint leave balance.
    /// </summary>
    public class LeaveRequestPdfRequest
    {
        public string FirstName { get; set; } = "";
        public string LastName { get; set; } = "";
        public string Department { get; set; } = "";
        public string IdNumber { get; set; } = "";
        public string EmployeeNumber { get; set; } = "";

        public string AddressDuringLeave { get; set; } = "";
        public string TelephoneNumber { get; set; } = "";

        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public double TotalDays { get; set; }

        public string LeaveTypeName { get; set; } = "";
        public string? OtherLeaveDescription { get; set; }

        public string? EmployeeSignature { get; set; }
        public DateTime? EmployeeSignatureDate { get; set; }

        public ManagerRecommendation ManagerRecommendation { get; set; }
        public bool? ManagerApprovedPaidLeave { get; set; }
        public string? ManagerRemarks { get; set; }
        public string? ManagerSignature { get; set; }
        public DateTime? ManagerDecisionDate { get; set; }

        public HrDecisionType HrDecision { get; set; }
        public string? HrRemarks { get; set; }
        public string? HrSignature { get; set; }
        public DateTime? HrDecisionDate { get; set; }

        public string? CapturedByName { get; set; }
        public DateTime? CapturedDate { get; set; }
        public string? VerifiedByName { get; set; }
        public DateTime? VerifiedDate { get; set; }

        /// <summary>Not tracked anywhere in the schema today - left blank on the form, same as
        /// TimesheetPdfService already does for "ID Number" when it has no source value.</summary>
        public string AreaBranchDivision { get; set; } = "";

        /// <summary>Every real employee here works for this South African company - the one constant
        /// fill-in on the form with no backing field at all.</summary>
        public string Country { get; set; } = "South Africa";

        /// <summary>Null when the leave type has no SharePoint balance column (e.g. Unpaid) or the
        /// employee has no matching LeaveInformation row - left blank on the form rather than printing 0.</summary>
        public double? LeaveAvailableDays { get; set; }
        public double? LeaveGrantedDays { get; set; }
        public double? LeaveBalanceDays { get; set; }
    }
}
