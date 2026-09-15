using PSS_Time_Tracker.Models;
using TimeSheetRecorder.Models;

namespace PSS_Time_Tracker.Services
{
    /// <summary>What resolved a given weekday - decides how it's labeled on the PDF and whether it
    /// still blocks report generation.</summary>
    public enum DayResolutionKind
    {
        /// <summary>A signed clock-in entry exists for this date - the normal case. Always wins over
        /// any GapResolution row that might also exist for the date (a real signature is authoritative).</summary>
        Worked,

        /// <summary>A clock-in entry exists but isn't signed yet, and no manager resolution has been
        /// recorded for the date either. Blocks report generation the same way it always has - not a
        /// data gap, just a missing signature. A manager CAN still resolve/bypass this (see
        /// ManagerController.ResolveGap) the same way as a true gap; once they do, the date's Kind
        /// becomes GapResolved instead, overriding the unsigned entry.</summary>
        NeedsSignature,

        /// <summary>An approved leave request (Status PendingPayrollCapture or Completed) covers this
        /// date.</summary>
        ApprovedLeave,

        /// <summary>A South African public holiday, and the employee didn't work it - standard pay,
        /// auto-signed, no manager action needed.</summary>
        PublicHolidayOff,

        /// <summary>A GapResolution row exists for this date with Status Confirmed or AutoResolved=true -
        /// a manager has explicitly resolved it (or Soft mode auto-resolved it), and that resolution
        /// takes priority over whatever else might be true about the date (an unsigned entry, a true
        /// gap, even an unworked holiday).</summary>
        GapResolved,

        /// <summary>The employee proposed a resolution for their own gap (TimeTrackerController.
        /// RequestGapResolution) and it's awaiting their manager's confirm/reject. Still blocks report
        /// generation in both Strict and Soft mode - an honest self-report shouldn't be silently
        /// overridden by Soft mode's automatic Unpaid Absence.</summary>
        PendingConfirmation,

        /// <summary>No clock-in, no approved leave, not a public holiday, no resolution on file (or the
        /// only one on file was Rejected) - a genuine data gap. Blocks Strict-mode report generation;
        /// Soft mode auto-resolves it as Unpaid Absence.</summary>
        TrueGap
    }

    /// <summary>One weekday's resolved status within a Generate Weekly Report date range.</summary>
    public class DayResolution
    {
        public DateTime Date { get; set; }
        public DayResolutionKind Kind { get; set; }

        public string TaskDescription { get; set; } = "";
        public DateTime? StartTime { get; set; }
        public DateTime? EndTime { get; set; }
        public double Hours { get; set; }
        public string? WorkLocation { get; set; }

        /// <summary>True if worked on a public holiday - shown as an overtime flag next to the task,
        /// on top of whichever Kind applies (a signed worked-on-holiday day is still Kind.Worked, just
        /// with this flag set, since a signed clock-in always PASSes on its own).</summary>
        public bool IsPublicHolidayOvertime { get; set; }

        /// <summary>What actually prints in the Signature column for this row - the employee's real
        /// typed signature for Worked/ApprovedLeave rows, or a clear system/administrative label for
        /// everything else (never the employee's real signature next to a day they didn't personally
        /// sign for).</summary>
        public string SignatureDisplay { get; set; } = "";

        /// <summary>Short annotation shown alongside the task for anything other than a plain worked
        /// day - e.g. "Paid", "Standard Pay", "Public Holiday - Overtime".</summary>
        public string? Note { get; set; }

        /// <summary>True for the statuses that still block report generation: an unresolved true gap, a
        /// real entry that isn't signed yet, or an employee-proposed resolution still awaiting the
        /// manager's confirm/reject.</summary>
        public bool RequiresAction =>
            Kind == DayResolutionKind.TrueGap ||
            Kind == DayResolutionKind.NeedsSignature ||
            Kind == DayResolutionKind.PendingConfirmation;

        public TimeTrackerModel? SourceEntry { get; set; }
        public GapResolution? SourceGapResolution { get; set; }
    }

    /// <summary>
    /// Runs the PASS/leave/holiday/gap waterfall over every weekday in a report's date range, for both
    /// the manager's Generate Report screen (ManagerController.GenerateEmployeeWeeklyReport - blocking)
    /// and the employee's own Track Your Time screen (TimeTrackerController.Index - informational, plus
    /// the self-service Holiday/Leave/Absent picker via RequestGapResolution).
    /// </summary>
    public interface IWeeklyReportGapAnalysisService
    {
        Task<List<DayResolution>> AnalyzeWeekAsync(string azureAdUserId, DateTime startDate, DateTime endDate);
    }
}
