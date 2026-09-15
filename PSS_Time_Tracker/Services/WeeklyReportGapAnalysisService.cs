using Microsoft.EntityFrameworkCore;
using PSS_Time_Tracker.Data;
using PSS_Time_Tracker.Models;

namespace PSS_Time_Tracker.Services
{
    /// <inheritdoc cref="IWeeklyReportGapAnalysisService"/>
    public class WeeklyReportGapAnalysisService : IWeeklyReportGapAnalysisService
    {
        private readonly timeSheetRecorderContext _context;
        private readonly IPublicHolidayService _publicHolidayService;

        public WeeklyReportGapAnalysisService(timeSheetRecorderContext context, IPublicHolidayService publicHolidayService)
        {
            _context = context;
            _publicHolidayService = publicHolidayService;
        }

        public async Task<List<DayResolution>> AnalyzeWeekAsync(string azureAdUserId, DateTime startDate, DateTime endDate)
        {
            var entries = await _context.TimeTracker
                .Where(t => t.AzureAdUserId == azureAdUserId && t.DateOfEntry >= startDate && t.DateOfEntry <= endDate)
                .ToListAsync();
            var entriesByDate = entries.ToDictionary(e => e.DateOfEntry.Date);

            var leaveRequests = await _context.LeaveRequests
                .Include(lr => lr.LeaveType)
                .Where(lr => lr.AzureAdUserId == azureAdUserId &&
                             (lr.Status == LeaveRequestStatus.PendingPayrollCapture || lr.Status == LeaveRequestStatus.Completed) &&
                             lr.StartDate <= endDate && lr.EndDate >= startDate)
                .ToListAsync();

            var gapResolutions = await _context.GapResolutions
                .Include(g => g.LeaveType)
                .Where(g => g.AzureAdUserId == azureAdUserId && g.Date >= startDate && g.Date <= endDate)
                .ToListAsync();
            var gapResolutionsByDate = gapResolutions.ToDictionary(g => g.Date.Date);

            var results = new List<DayResolution>();

            for (var date = startDate.Date; date <= endDate.Date; date = date.AddDays(1))
            {
                // The rest of the app only ever deals in Monday-Friday working weeks (see
                // TimeTrackerController.Create's date-range validation) - weekends never need a
                // resolution of any kind.
                if (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday)
                {
                    continue;
                }

                results.Add(await ResolveDayAsync(date, entriesByDate, leaveRequests, gapResolutionsByDate));
            }

            return results;
        }

        private async Task<DayResolution> ResolveDayAsync(
            DateTime date,
            Dictionary<DateTime, TimeSheetRecorder.Models.TimeTrackerModel> entriesByDate,
            List<LeaveRequest> leaveRequests,
            Dictionary<DateTime, GapResolution> gapResolutionsByDate)
        {
            entriesByDate.TryGetValue(date, out var entry);
            var isHoliday = await _publicHolidayService.IsPublicHolidayAsync(date);

            // Step 1: a signed clock-in entry always PASSes on its own, holiday or not - it's just
            // flagged as overtime if it happens to land on a public holiday. A real signature always
            // wins, even over an existing gap resolution for the same date.
            if (entry != null && !string.IsNullOrWhiteSpace(entry.Signature))
            {
                return new DayResolution
                {
                    Date = date,
                    Kind = DayResolutionKind.Worked,
                    TaskDescription = entry.DailyTask ?? "",
                    StartTime = entry.StartTime,
                    EndTime = entry.EndTime,
                    Hours = entry.TotalHrsWorked,
                    WorkLocation = entry.WorkLocation,
                    IsPublicHolidayOvertime = isHoliday,
                    SignatureDisplay = entry.Signature,
                    Note = isHoliday ? "Public Holiday - Overtime" : null,
                    SourceEntry = entry
                };
            }

            // Step 2: approved leave covering this date.
            var leave = leaveRequests.FirstOrDefault(lr => lr.StartDate.Date <= date && lr.EndDate.Date >= date);
            if (leave != null)
            {
                bool paid = leave.HrDecision != HrDecisionType.ApprovedWithoutPay;
                return new DayResolution
                {
                    Date = date,
                    Kind = DayResolutionKind.ApprovedLeave,
                    TaskDescription = $"Approved Leave - {leave.LeaveType?.Name ?? "Leave"}",
                    Hours = 8,
                    SignatureDisplay = "(Approved Leave - See Leave Record)",
                    Note = paid ? "Paid" : "Unpaid"
                };
            }

            // Step 3: a resolution already on file for this date - a manager's direct decision (or Soft
            // mode's auto-resolution) overrides everything below, including an unsigned entry sitting
            // there or an unworked holiday. This is the "manager can bypass Awaiting Signature" path.
            if (gapResolutionsByDate.TryGetValue(date, out var resolution) && resolution.Status != GapResolutionStatus.Rejected)
            {
                var methodLabel = ResolutionLabel(resolution);

                if (resolution.Status == GapResolutionStatus.Pending)
                {
                    return new DayResolution
                    {
                        Date = date,
                        Kind = DayResolutionKind.PendingConfirmation,
                        TaskDescription = resolution.Notes is { Length: > 0 } ? resolution.Notes : methodLabel,
                        Hours = resolution.Hours,
                        SignatureDisplay = "(Awaiting Manager Confirmation)",
                        Note = $"Employee Proposed: {methodLabel}",
                        SourceGapResolution = resolution
                    };
                }

                string note = resolution.AutoResolved
                    ? $"{methodLabel} (Auto-Resolved - Soft Mode)"
                    : $"{methodLabel} (Gap Resolved)";

                return new DayResolution
                {
                    Date = date,
                    Kind = DayResolutionKind.GapResolved,
                    TaskDescription = resolution.Notes is { Length: > 0 } ? resolution.Notes : note,
                    Hours = resolution.Hours,
                    SignatureDisplay = "(Resolved by Manager)",
                    Note = note,
                    SourceGapResolution = resolution
                };
            }

            // Step 4: public holiday.
            if (isHoliday)
            {
                if (entry != null)
                {
                    // Worked it, but that entry isn't signed yet - still needs the employee's
                    // signature (or a manager's override, per Step 3) before this date can PASS.
                    return new DayResolution
                    {
                        Date = date,
                        Kind = DayResolutionKind.NeedsSignature,
                        TaskDescription = entry.DailyTask ?? "",
                        StartTime = entry.StartTime,
                        EndTime = entry.EndTime,
                        Hours = entry.TotalHrsWorked,
                        WorkLocation = entry.WorkLocation,
                        IsPublicHolidayOvertime = true,
                        SignatureDisplay = "",
                        Note = "Public Holiday - Overtime, awaiting employee signature",
                        SourceEntry = entry
                    };
                }

                return new DayResolution
                {
                    Date = date,
                    Kind = DayResolutionKind.PublicHolidayOff,
                    TaskDescription = "Public Holiday - Not Worked",
                    Hours = 8,
                    SignatureDisplay = "(Auto-Signed - Public Holiday)",
                    Note = "Standard Pay"
                };
            }

            // Real entry exists for this date but isn't a holiday and isn't signed - needs signing (or
            // a manager override, per Step 3), not gap resolution.
            if (entry != null)
            {
                return new DayResolution
                {
                    Date = date,
                    Kind = DayResolutionKind.NeedsSignature,
                    TaskDescription = entry.DailyTask ?? "",
                    StartTime = entry.StartTime,
                    EndTime = entry.EndTime,
                    Hours = entry.TotalHrsWorked,
                    WorkLocation = entry.WorkLocation,
                    SignatureDisplay = "",
                    Note = "Awaiting employee signature",
                    SourceEntry = entry
                };
            }

            // Step 5: no clock-in, no approved leave, not a public holiday, no (non-rejected) resolution
            // on file - a true gap.
            return new DayResolution
            {
                Date = date,
                Kind = DayResolutionKind.TrueGap,
                TaskDescription = "",
                Hours = 0,
                SignatureDisplay = "",
                Note = "Unresolved Gap - No Clock-In, No Leave, Not a Public Holiday"
            };
        }

        internal static string MethodLabel(GapResolutionMethod method) => method switch
        {
            GapResolutionMethod.UnpaidAbsence => "Unpaid Absence",
            GapResolutionMethod.RetroactiveLeave => "Retroactive Leave",
            GapResolutionMethod.ManualPunch => "Manual Punch (Entered by Manager)",
            GapResolutionMethod.Holiday => "Holiday",
            _ => "Resolved"
        };

        /// <summary>Same as MethodLabel, but names the specific leave type when one was picked - e.g.
        /// "Sick Leave" instead of the generic "Retroactive Leave" - for a day flagged straight from
        /// Capture Your Timesheet (TimeTrackerController.Create) or the Timesheet Gaps self-service
        /// picker (RequestGapResolution).</summary>
        internal static string ResolutionLabel(GapResolution resolution) =>
            resolution.Method == GapResolutionMethod.RetroactiveLeave && resolution.LeaveType != null
                ? $"{resolution.LeaveType.Name} Leave"
                : MethodLabel(resolution.Method);
    }
}
