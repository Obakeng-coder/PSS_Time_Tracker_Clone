namespace PSS_Time_Tracker.Models
{
    /// <summary>
    /// Where a leave request currently sits in the 3-stage sign-off chain from the real Employee
    /// Leave Application form: Reporting Manager -&gt; HR Manager -&gt; Administration/Payroll.
    /// </summary>
    public enum LeaveRequestStatus
    {
        PendingManager = 0,
        RejectedByManager = 1,
        PendingHR = 2,
        RejectedByHR = 3,
        PendingPayrollCapture = 4,
        Completed = 5,
        Cancelled = 6
    }

    /// <summary>Matches the "Recommendation by Reporting Manager" checkboxes on the paper form.</summary>
    public enum ManagerRecommendation
    {
        None = 0,
        Recommended = 1,
        NotRecommended = 2,
        RescheduleRequested = 3
    }

    /// <summary>Matches the "Approved by HR Manager" checkboxes on the paper form.</summary>
    public enum HrDecisionType
    {
        None = 0,
        ApprovedWithPay = 1,
        ApprovedWithoutPay = 2,
        NotApproved = 3
    }
}
