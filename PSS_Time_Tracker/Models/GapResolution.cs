using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PSS_Time_Tracker.Models
{
    /// <summary>How a gap day is being resolved. ManualPunch is manager-only (attesting to hours with
    /// no employee-signed record) - an employee's self-service picker only ever offers Holiday, Leave,
    /// or Absent. LateSubmission is system-created, never picked by anyone directly: it's set the
    /// moment an employee backfills a day that was an unresolved gap with a real, signed worked-day
    /// entry via TimeTrackerController.Create - the entry itself is real (unlike every other method
    /// here, which has no underlying TimeTracker row), but still needs the manager's sign-off before
    /// it counts as resolved, same as any other proposed resolution.</summary>
    public enum GapResolutionMethod
    {
        UnpaidAbsence = 0,
        RetroactiveLeave = 1,
        ManualPunch = 2,
        Holiday = 3,
        LateSubmission = 4
    }

    /// <summary>Confirmed/AutoResolved both count as resolved for report-generation purposes - the only
    /// difference is whether a manager explicitly chose it (Confirmed) or Soft mode auto-assigned it
    /// (see AutoResolved). Pending still blocks Strict mode (and, unlike a true gap, Soft mode too - an
    /// employee's honest self-report shouldn't be silently overridden by an automatic Unpaid Absence).
    /// Rejected falls back to being treated as if unresolved.</summary>
    public enum GapResolutionStatus
    {
        Pending,
        Confirmed,
        Rejected
    }

    /// <summary>
    /// One row per (employee, gap date) - either a manager resolving it directly (Status set straight to
    /// Confirmed), an employee proposing a resolution for their own gap (Status starts Pending, via
    /// TimeTrackerController.RequestGapResolution), or Soft mode auto-resolving a true gap at report
    /// generation time (AutoResolved = true, Status Confirmed). Unique on (AzureAdUserId, Date) - a new
    /// submission for the same date overwrites the previous one.
    /// </summary>
    public class GapResolution
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string AzureAdUserId { get; set; } = "";

        [Required]
        public DateTime Date { get; set; }

        [Required]
        public GapResolutionMethod Method { get; set; }

        /// <summary>Hours credited for this date: 0 for Unpaid Absence, the standard workday for
        /// Retroactive Leave/Holiday, the manager-entered figure for Manual Punch.</summary>
        public double Hours { get; set; }

        /// <summary>Which specific leave type this was, when Method is RetroactiveLeave and the
        /// employee (or manager) named one - e.g. flagging a day as Sick Leave directly from Capture
        /// Your Timesheet (TimeTrackerController.Create) instead of filing a formal leave request. Null
        /// for a generic "Leave" pick with no specific type, and for every other method.</summary>
        public int? LeaveTypeId { get; set; }

        [ForeignKey(nameof(LeaveTypeId))]
        public LeaveType? LeaveType { get; set; }

        /// <summary>Manual Punch: the task/reason a manager entered on the employee's behalf. Other
        /// methods: an optional note from whoever proposed this resolution.</summary>
        public string? Notes { get; set; }

        [Required]
        public string RequestedByUserId { get; set; } = "";

        public DateTime RequestedDate { get; set; } = DateTime.Now;

        [Required]
        public GapResolutionStatus Status { get; set; } = GapResolutionStatus.Confirmed;

        /// <summary>Set once a manager confirms or rejects an employee-proposed (Pending) resolution.
        /// Null for a manager's own direct resolution (self-confirmed) and for AutoResolved rows.</summary>
        public string? ConfirmedByUserId { get; set; }
        public DateTime? ConfirmedDate { get; set; }

        /// <summary>True only for Soft-mode auto-resolution at report-generation time - always
        /// Status.Confirmed, never needs a manager's explicit click.</summary>
        public bool AutoResolved { get; set; }
    }
}
