using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TimeSheetRecorder.Models;
using TimeSheetRecorder.Models.ViewModels;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using PSS_Time_Tracker.Data;
using PSS_Time_Tracker.Models;
using PSS_Time_Tracker.Services;

namespace PSS_Time_Tracker.Controllers
{
    [Authorize]
    public class TimeTrackerController : Controller
    {
        private readonly timeSheetRecorderContext _context;
        private readonly ILogger<TimeTrackerController> _logger;
        private readonly ITimesheetPdfService _pdfService;
        private readonly ISharePointCheckInService _sharePointService;
        private readonly IAzureAdProfileService _azureAdProfileService;
        private readonly IWeeklyReportGapAnalysisService _gapAnalysisService;
        private readonly INotificationService _notificationService;

        public TimeTrackerController(
            timeSheetRecorderContext context,
            ILogger<TimeTrackerController> logger,
            ITimesheetPdfService pdfService,
            ISharePointCheckInService sharePointService,
            IAzureAdProfileService azureAdProfileService,
            IWeeklyReportGapAnalysisService gapAnalysisService,
            INotificationService notificationService)
        {
            _context = context;
            _logger = logger;
            _pdfService = pdfService;
            _sharePointService = sharePointService;
            _azureAdProfileService = azureAdProfileService;
            _gapAnalysisService = gapAnalysisService;
            _notificationService = notificationService;
        }

        // SharePoint's Title column (and "Reports to") store a full name as one string, e.g.
        // "Samantha Kgatla Moyagrabo". The last word is always the surname, regardless of how many
        // given names precede it - so "Samantha Kgatla" / "Moyagrabo", not "Samantha" / "Kgatla
        // Moyagrabo". Used wherever a SharePoint full name needs to become (EmployeeName, EmployeeSurname).
        private static (string GivenNames, string Surname) SplitFullName(string fullName)
        {
            var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return parts.Length switch
            {
                0 => ("", ""),
                1 => (parts[0], ""),
                _ => (string.Join(' ', parts[..^1]), parts[^1])
            };
        }

        // Prefers the employee's real Azure AD Job Title over the local UserAccount value, closer to
        // how the original (pre-clone) app worked. Falls back gracefully - the local seeded test
        // accounts use @local.test addresses that don't resolve to any real Azure AD user, so this
        // simply returns null for them and the local value is used instead.
        private async Task<string> GetEffectiveJobTitleAsync(PSS_Time_Tracker.Models.UserAccount? userAccount)
        {
            if (userAccount == null)
            {
                return ".";
            }

            var profile = await _azureAdProfileService.GetProfileAsync(userAccount.Email);
            return !string.IsNullOrWhiteSpace(profile?.JobTitle) ? profile!.JobTitle! : (userAccount.JobTitle ?? ".");
        }

        /// <summary>
        /// <paramref name="date"/> lets a notification (e.g. a manager's Request Timesheet - see
        /// ManagerController.RequestTimesheetFromEmployee) deep-link straight to the day it's about,
        /// instead of always defaulting to today. If that day already has an entry, editing it is more
        /// useful than a fresh Create form that would just reject as a duplicate on submit - see Edit.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> Create(DateTime? date)
        {
            var userId = User.GetUserId();
            var targetDate = date?.Date ?? DateTime.Today;

            var existingEntry = await _context.TimeTracker.FirstOrDefaultAsync(t =>
                t.AzureAdUserId == userId && t.DateOfEntry.Date == targetDate);
            if (existingEntry != null)
            {
                return RedirectToAction(nameof(Edit), new { id = existingEntry.TimeTrackerId });
            }

            var userAccount = await _context.Users.FirstOrDefaultAsync(u => u.AzureAdUserId == userId);

            // The mock/real check-in service only ever has a record for the actual current date (see
            // MockSharePointCheckInService) - requesting a past date within the week correctly shows "no
            // check-in" rather than fabricating one, same as if the employee navigated here directly.
            var checkIn = userAccount != null
                ? await _sharePointService.GetCheckInForDateAsync($"{userAccount.EmployeeName} {userAccount.EmployeeSurname}", targetDate)
                : null;

            if (userAccount != null)
            {
                await SyncManagerFromSharePointAsync(userAccount, checkIn);
            }

            var (employeeName, employeeSurname) = checkIn?.EmployeeName is { Length: > 0 } spName
                ? SplitFullName(spName)
                : (userAccount?.EmployeeName ?? "", userAccount?.EmployeeSurname ?? "");

            var model = new TimeTrackerViewModel
            {
                EmployeeName = employeeName,
                EmployeeSurname = employeeSurname,
                JobTitle = await GetEffectiveJobTitleAsync(userAccount),
                SupervisorFullName = userAccount?.SupervisorFullName ?? ".",
                AzureAdUserId = userId,
                DateOfEntry = targetDate,
                TimeSheetMonth = "",
                StartTime = checkIn?.CheckInTime,
                EndTime = checkIn?.CheckOutTime,
                // Read-only, always pulled from SharePoint - never a dropdown the employee picks from.
                WorkLocation = checkIn?.WorkLocation ?? "",
                HasSharePointCheckIn = checkIn != null,
                LeaveTypeOptions = await _context.LeaveTypes.OrderBy(lt => lt.Name).ToListAsync()
            };

            return View(model);
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(TimeTrackerViewModel viewModel)
        {
            viewModel.TimeSheetMonth = viewModel.DateOfEntry.ToString("MMMM yyyy");

            var userId = User.GetUserId();

            // "Day Type" dropdown: Regular / PublicHoliday / a LeaveTypeId. Picking a leave type diverts
            // entirely to a lightweight self-service flag (no SharePoint check-in, no worked-day fields
            // required) instead of the normal worked-day flow below - see HandleLeaveFlagAsync.
            if (int.TryParse(viewModel.DayType, out var selectedLeaveTypeId))
            {
                return await HandleLeaveFlagAsync(viewModel, userId, selectedLeaveTypeId);
            }

            viewModel.IsPublicHoliday = viewModel.DayType == "PublicHoliday";

            var userAccount = await _context.Users.FirstOrDefaultAsync(u => u.AzureAdUserId == userId);

            // Start/End Time are never trusted from the posted form - always re-derived from
            // SharePoint for the selected date, since that's the whole point of pulling them "every
            // time" rather than letting the employee type them.
            var checkIn = !viewModel.IsPublicHoliday && userAccount != null
                ? await _sharePointService.GetCheckInForDateAsync($"{userAccount.EmployeeName} {userAccount.EmployeeSurname}", viewModel.DateOfEntry)
                : null;

            // Work Location is never trusted from the posted form either - same rule as Start/End
            // Time. The form now renders it read-only, but that alone doesn't stop a tampered POST, so
            // it's overwritten here regardless of what (if anything) was submitted. The [Required]
            // attribute may have already flagged the raw posted value in ModelState before this code
            // ran; clear that now that we're about to set the real value ourselves.
            ModelState.Remove(nameof(viewModel.WorkLocation));

            double totalHoursDecimal;
            if (viewModel.IsPublicHoliday)
            {
                viewModel.WorkLocation = "PSS";
                totalHoursDecimal = 8;
                viewModel.StartTime = viewModel.DateOfEntry.Date;
                viewModel.EndTime = viewModel.DateOfEntry.Date;
                viewModel.DailyTask = "Public Holiday";
            }
            else if (checkIn?.CheckInTime.HasValue == true && checkIn.CheckOutTime.HasValue)
            {
                viewModel.StartTime = checkIn.CheckInTime;
                viewModel.EndTime = checkIn.CheckOutTime;
                viewModel.WorkLocation = checkIn.WorkLocation ?? "";
                totalHoursDecimal = Math.Round((checkIn.CheckOutTime.Value - checkIn.CheckInTime.Value).TotalHours, 2);
            }
            else
            {
                viewModel.WorkLocation = "";
                totalHoursDecimal = 0;
                ModelState.AddModelError("", "No check-in/check-out record was found in SharePoint for this date yet. " +
                    "Please check in via the mobile app first, then submit your timesheet.");
            }

            if (viewModel.DateOfEntry.DayOfWeek == System.DayOfWeek.Saturday ||
                viewModel.DateOfEntry.DayOfWeek == System.DayOfWeek.Sunday)
            {
                ModelState.AddModelError("DateOfEntry", "You can only select dates from Monday to Friday.");
            }


            // Current week is always open; an earlier date is only open when it's itself an unresolved
            // gap being caught up, and even then only while no OTHER earlier day still needs attention
            // (see ValidateAgainstUnresolvedGapsAsync's doc comment for the full replaced-approval-flow
            // rationale). isBackfill is used further down, once the entry has actually saved, to flag
            // it for the manager's confirmation.
            var (gapError, isBackfill) = await ValidateAgainstUnresolvedGapsAsync(userId, viewModel.DateOfEntry);
            if (gapError != null)
            {
                ModelState.AddModelError("DateOfEntry", gapError);
            }

            if (!viewModel.IsPublicHoliday &&
                viewModel.StartTime.HasValue && viewModel.EndTime.HasValue &&
                viewModel.EndTime.Value <= viewModel.StartTime.Value)
            {
                ModelState.AddModelError("", "End Time must be after Start Time.");
            }

            if (string.IsNullOrWhiteSpace(viewModel.WorkLocation))
            {
                ModelState.AddModelError(nameof(viewModel.WorkLocation),
                    "No Work Location was found on your SharePoint check-in record for this date.");
            }

            if (string.IsNullOrWhiteSpace(viewModel.DailyTask) && !viewModel.IsPublicHoliday)
            {
                ModelState.AddModelError(nameof(viewModel.DailyTask), "Daily Task is required.");
            }

            // Signature being a non-nullable string already gets ASP.NET Core's implicit required
            // validation ("The Signature field is required.") when it's missing/blank - no need to
            // duplicate that with a manual check here.

            if (!ModelState.IsValid)
            {
                // Collect all validation errors
                var errorMessages = ModelState.Values
                    .SelectMany(v => v.Errors)
                    .Select(e => e.ErrorMessage)
                    .ToList();

               
                return Json(new
                {
                    showValidationError = true,
                    errorMessages = errorMessages
                });
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // First check for duplicate entry for this date
                    var existingEntry = await _context.TimeTracker
                        .FirstOrDefaultAsync(t => t.AzureAdUserId == userId &&
                                                t.DateOfEntry.Date == viewModel.DateOfEntry.Date);

                    if (existingEntry != null)
                    {
                        TempData["DuplicateDateError"] = $"You have already submitted a timesheet for {viewModel.DateOfEntry:yyyy-MM-dd}.";
                        return Json(new
                        {
                            showDuplicateModal = true,
                            errorMessage = TempData["DuplicateDateError"]
                        });
                    }

                    // A day already covered by a pending/approved leave request can't also be logged
                    // as worked - the reciprocal of the check in LeaveController.Create.
                    var onLeaveThatDay = await _context.LeaveRequests.AnyAsync(lr =>
                        lr.AzureAdUserId == userId &&
                        lr.Status != LeaveRequestStatus.RejectedByManager &&
                        lr.Status != LeaveRequestStatus.RejectedByHR &&
                        lr.Status != LeaveRequestStatus.Cancelled &&
                        lr.StartDate.Date <= viewModel.DateOfEntry.Date &&
                        lr.EndDate.Date >= viewModel.DateOfEntry.Date);

                    if (onLeaveThatDay)
                    {
                        return Json(new
                        {
                            showValidationError = true,
                            errorMessages = new[] { $"You have a leave request covering {viewModel.DateOfEntry:yyyy-MM-dd}. " +
                                "A timesheet entry can't be logged for a day already taken as leave." }
                        });
                    }

                    // Employee/manager details now come from the UserAccount row created at
                    // login time, instead of a live Microsoft Graph lookup - except the name, which
                    // (when a SharePoint check-in exists) is re-split from the same Title string shown
                    // on the form, so what's saved matches what the employee saw, not a separately
                    // stored local value.
                    var (savedEmployeeName, savedEmployeeSurname) = checkIn?.EmployeeName is { Length: > 0 } spEmployeeName
                        ? SplitFullName(spEmployeeName)
                        : (userAccount?.EmployeeName ?? "", userAccount?.EmployeeSurname ?? "");

                    var timeTracker = new TimeTrackerModel
                    {
                        AzureAdUserId = userId,
                        EmployeeName = savedEmployeeName,
                        EmployeeSurname = savedEmployeeSurname,
                        JobTitle = await GetEffectiveJobTitleAsync(userAccount),
                        SupervisorFullName = userAccount?.SupervisorFullName ?? ".",
                        WorkLocation = viewModel.WorkLocation,
                        TimeSheetMonth = viewModel.TimeSheetMonth,
                        DateOfEntry = viewModel.DateOfEntry,
                        StartTime = viewModel.StartTime.Value,
                        EndTime = viewModel.EndTime.Value,
                        TotalHrsWorked = totalHoursDecimal,
                        DailyTask = viewModel.DailyTask,
                        Signature = viewModel.Signature,
                        IsPublicHoliday = viewModel.IsPublicHoliday
                    };

                    var (flagged, flagReason) = DetectAnomalies(timeTracker);
                    timeTracker.IsFlaggedForReview = flagged;
                    timeTracker.FlagReason = flagReason;

                    _context.TimeTracker.Add(timeTracker);
                    await _context.SaveChangesAsync();

                    // This entry just filled what was, until now, an unresolved gap (see
                    // ValidateAgainstUnresolvedGapsAsync) - flag it for the manager's confirmation
                    // before it counts as resolved, same as any other proposed resolution, and let
                    // them know. WeeklyReportGapAnalysisService shows this date as PendingConfirmation
                    // (not plain Worked) while that's outstanding.
                    if (isBackfill)
                    {
                        await UpsertPendingGapResolutionAsync(
                            userId, viewModel.DateOfEntry, GapResolutionMethod.LateSubmission, null,
                            "Filled in after initially missing this day.", MondayOf(viewModel.DateOfEntry),
                            hoursOverride: totalHoursDecimal);
                    }

                    return Json(new { redirectUrl = Url.Action(nameof(Index)) });
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Database error while saving timesheet");
                    ModelState.AddModelError("", "An error occurred while saving your time entry.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Unexpected error while saving timesheet");
                    ModelState.AddModelError("", "An unexpected error occurred. Please try again.");
                }
            }

            if (!ModelState.IsValid)
            {
                foreach (var kvp in ModelState)
                {
                    foreach (var error in kvp.Value.Errors)
                    {
                        _logger.LogWarning("ModelState error for {Key}: {ErrorMessage}", kvp.Key, error.ErrorMessage);
                    }
                }
            }

            return View(viewModel);
        }

        /// <summary>The "Day Type" dropdown's leave-type branch of Create: flags a specific date as
        /// Annual/Sick/Family Responsibility/Unpaid/Other leave straight from Capture Your Timesheet,
        /// rather than filing a formal Request Time Off application. This never creates a LeaveRequest -
        /// it's a lightweight notice to the manager (the same Pending gap-resolution proposal a self
        /// service pick from the Timesheet Gaps panel produces - see RequestGapResolution), who confirms
        /// or rejects it from the Timesheet Gaps panel like any other proposal.</summary>
        private async Task<IActionResult> HandleLeaveFlagAsync(TimeTrackerViewModel viewModel, string userId, int leaveTypeId)
        {
            var errors = new List<string>();

            if (viewModel.DateOfEntry.DayOfWeek == System.DayOfWeek.Saturday ||
                viewModel.DateOfEntry.DayOfWeek == System.DayOfWeek.Sunday)
            {
                errors.Add("You can only select dates from Monday to Friday.");
            }

            // Same relaxed rule as the worked-day path (ValidateAgainstUnresolvedGapsAsync) - current
            // week is always open, or an earlier date specifically if it's still an unresolved gap
            // being flagged as leave instead of clocked. isBackfill is unused here (unlike Create's
            // worked-day path): flagging IS the resolution, so there's nothing further to mark once
            // UpsertPendingGapResolutionAsync runs below.
            var (gapError, _) = await ValidateAgainstUnresolvedGapsAsync(userId, viewModel.DateOfEntry);
            if (gapError != null)
            {
                errors.Add(gapError);
            }

            if (string.IsNullOrWhiteSpace(viewModel.Signature))
            {
                errors.Add("Signature is required to confirm you were on leave that day.");
            }

            var leaveType = await _context.LeaveTypes.FindAsync(leaveTypeId);
            if (leaveType == null)
            {
                errors.Add("Please choose a valid leave type.");
            }

            if (errors.Any())
            {
                return Json(new { showValidationError = true, errorMessages = errors });
            }

            // Can't flag a day that already has a real entry, or is already covered by a formal leave
            // request - same overlap rule as the normal worked-day path.
            var existingEntry = await _context.TimeTracker.FirstOrDefaultAsync(t =>
                t.AzureAdUserId == userId && t.DateOfEntry.Date == viewModel.DateOfEntry.Date);
            if (existingEntry != null)
            {
                return Json(new
                {
                    showValidationError = true,
                    errorMessages = new[] { $"You already have a timesheet entry for {viewModel.DateOfEntry:yyyy-MM-dd}." }
                });
            }

            var onLeaveThatDay = await _context.LeaveRequests.AnyAsync(lr =>
                lr.AzureAdUserId == userId &&
                lr.Status != LeaveRequestStatus.RejectedByManager &&
                lr.Status != LeaveRequestStatus.RejectedByHR &&
                lr.Status != LeaveRequestStatus.Cancelled &&
                lr.StartDate.Date <= viewModel.DateOfEntry.Date &&
                lr.EndDate.Date >= viewModel.DateOfEntry.Date);
            if (onLeaveThatDay)
            {
                return Json(new
                {
                    showValidationError = true,
                    errorMessages = new[] { $"You already have a formal leave request covering {viewModel.DateOfEntry:yyyy-MM-dd}." }
                });
            }

            var weekStart = MondayOf(viewModel.DateOfEntry);
            var dayResolutions = await _gapAnalysisService.AnalyzeWeekAsync(userId, weekStart, weekStart.AddDays(4));
            var day = dayResolutions.FirstOrDefault(d => d.Date.Date == viewModel.DateOfEntry.Date);
            if (day == null || day.Kind != DayResolutionKind.TrueGap)
            {
                return Json(new
                {
                    showValidationError = true,
                    errorMessages = new[] { $"{viewModel.DateOfEntry:yyyy-MM-dd} is already accounted for - nothing to flag." }
                });
            }

            await UpsertPendingGapResolutionAsync(
                userId, viewModel.DateOfEntry, GapResolutionMethod.RetroactiveLeave, leaveTypeId, viewModel.LeaveReason, weekStart);

            TempData["SuccessMessage"] = $"{viewModel.DateOfEntry:yyyy-MM-dd} flagged as {leaveType!.Name} Leave - " +
                "awaiting your manager's confirmation. This does not replace a formal Request Time Off application.";
            return Json(new { redirectUrl = Url.Action(nameof(Index)) });
        }



        public async Task<IActionResult> Index(DateTime? startDate, DateTime? endDate, int page = 1)
        {
            var userId = User.GetUserId();
            int pageSize = 10; // Number of entries per page

            var timeEntries = await _context.TimeTracker
         .FromSqlInterpolated($"EXEC GetUserTimeEntriesPaged @AzureAdUserId={userId}, @StartDate={startDate}, @EndDate={endDate}, @PageNumber={page}, @PageSize={pageSize}")
         .AsNoTracking()
         .ToListAsync();

            var totalRecords = _context.SpResults
                .FromSqlInterpolated($"EXEC GetUserTimeEntriesCount @AzureAdUserId={userId}, @StartDate={startDate}, @EndDate={endDate}")
                .AsNoTracking()
                .AsEnumerable() // 👈 move execution to client side
                .Select(r => r.TotalCount)
                .FirstOrDefault();



            var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

            // Set ViewBag values for date filters
            if (startDate.HasValue)
            {
                ViewBag.StartDate = startDate.Value.ToString("yyyy-MM-dd");
            }

            if (endDate.HasValue)
            {
                ViewBag.EndDate = endDate.Value.ToString("yyyy-MM-dd");
            }

            // Check for complete week if dates are filtered
            bool showReportButton = false;
            if (startDate.HasValue && endDate.HasValue)
            {
                // Check if the filtered range is exactly one week (Monday to Friday)
                TimeSpan span = endDate.Value - startDate.Value;
                if (span.Days == 4 && startDate.Value.DayOfWeek == System.DayOfWeek.Monday && endDate.Value.DayOfWeek == System.DayOfWeek.Friday)
                {
                    // Check if we have entries for all weekdays
                    var datesInRange = Enumerable.Range(0, 5)
                        .Select(offset => startDate.Value.AddDays(offset))
                        .ToList();

                    var entryDates = timeEntries
                        .Select(e => e.DateOfEntry.Date)
                        .Distinct()
                        .ToList();

                    showReportButton = datesInRange.All(d => entryDates.Contains(d));
                }
            }

            ViewBag.ShowReportButton = showReportButton;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.HasPrevious = page > 1;
            ViewBag.HasNext = page < totalPages;

            // Informational + self-service only on this side - an employee's own gaps never block
            // their own report the way they block the manager's (see ManagerController.
            // GenerateEmployeeWeeklyReport). Lets them see and propose a resolution (Holiday/Leave/
            // Absent) for their own gap days; a manager still has to confirm it (RequestGapResolution
            // below, ManagerController.ConfirmGapResolution).
            if (startDate.HasValue && endDate.HasValue)
            {
                ViewBag.DayResolutions = await _gapAnalysisService.AnalyzeWeekAsync(userId, startDate.Value, endDate.Value);
            }

            return View(timeEntries);
        }

        /// <summary>Employee self-service for their own gap day - Holiday, (Retroactive) Leave, or
        /// (Unpaid) Absent only; unlike a manager's ResolveGap this never includes Manual Punch, since
        /// that's a manager attesting to hours with no employee-signed record, not something an
        /// employee can do for themselves. Always lands as Pending - a manager has to confirm it before
        /// it counts as resolved (ManagerController.ConfirmGapResolution).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestGapResolution(DateTime date, GapResolutionMethod method, int? leaveTypeId, string? notes)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrEmpty(userId) || date == default)
            {
                return BadRequest();
            }

            var weekStart = MondayOf(date);

            if (method == GapResolutionMethod.ManualPunch)
            {
                TempData["ErrorMessage"] = "Manual Punch can only be entered by your manager.";
                return RedirectToAction(nameof(Index), new { startDate = weekStart, endDate = weekStart.AddDays(4) });
            }

            // Only a genuine gap (or a previously rejected proposal) can be self-resolved - can't
            // propose a resolution for a day that's already worked, on approved leave, a public
            // holiday, or already has a pending/confirmed resolution on file.
            var dayResolutions = await _gapAnalysisService.AnalyzeWeekAsync(userId, weekStart, weekStart.AddDays(4));
            var day = dayResolutions.FirstOrDefault(d => d.Date.Date == date.Date);
            if (day == null || day.Kind != DayResolutionKind.TrueGap)
            {
                TempData["ErrorMessage"] = $"{date:yyyy-MM-dd} isn't an open gap - nothing to propose.";
                return RedirectToAction(nameof(Index), new { startDate = weekStart, endDate = weekStart.AddDays(4) });
            }

            await UpsertPendingGapResolutionAsync(userId, date, method,
                method == GapResolutionMethod.RetroactiveLeave ? leaveTypeId : null, notes, weekStart);

            TempData["SuccessMessage"] = $"{date:yyyy-MM-dd} submitted - awaiting your manager's confirmation.";
            return RedirectToAction(nameof(Index), new { startDate = weekStart, endDate = weekStart.AddDays(4) });
        }

        /// <summary>Same Monday-of-week formula used throughout this controller.</summary>
        private static DateTime MondayOf(DateTime date)
        {
            var mondayOffset = date.DayOfWeek == System.DayOfWeek.Sunday ? -6 : (int)System.DayOfWeek.Monday - (int)date.DayOfWeek;
            return date.AddDays(mondayOffset);
        }

        /// <summary>How far back an employee can still be blocked by (or backfill) an unresolved gap -
        /// not their whole history, which would make a years-old gap that predates this feature block
        /// them forever. 90 days is generous for a genuine "I missed a week or two" catch-up without
        /// scanning unbounded history on every submission.</summary>
        private const int GapLookbackDays = 90;

        /// <summary>
        /// Replaces the old "current week only, or request admin approval to bypass" rule (removed
        /// along with Admin_Approve_EmployeeController - employees now come from Azure AD/SharePoint,
        /// already-known and already-vetted, so there's no "unknown employee" case left to gate on).
        /// An employee may always submit for the current week, or backfill a specific earlier date
        /// that's still an unresolved gap - exactly "catching up a missed day", nothing else. But while
        /// ANY earlier day still needs attention (a true gap, or a real entry still missing its
        /// signature), they're blocked from moving on to the current/a new week until it's dealt with -
        /// either caught up directly here, or resolved by their manager (ManagerController.ResolveGap /
        /// ConfirmGapResolution).
        /// </summary>
        /// <returns>Null if <paramref name="dateOfEntry"/> is allowed; otherwise the error to show.
        /// <paramref name="isBackfill"/> is true only when it's allowed specifically because it's a
        /// genuine unresolved gap being caught up - the caller uses this to know whether to flag the
        /// entry for the manager's confirmation once it saves (see UpsertPendingGapResolutionAsync).</paramref>
        private async Task<(string? Error, bool IsBackfill)> ValidateAgainstUnresolvedGapsAsync(string userId, DateTime dateOfEntry)
        {
            var currentMonday = MondayOf(DateTime.Today);
            var currentFriday = currentMonday.AddDays(4);

            if (dateOfEntry >= currentMonday && dateOfEntry <= currentFriday)
            {
                // Bounded to whichever is later: the lookback window, or this employee's first-ever
                // entry - otherwise someone brand new (or newly onboarded from Azure AD, with no
                // history in this system at all yet) would have their entire pre-employment past
                // flagged as "gaps" the moment they submit their first timesheet. No entries at all
                // ever - nothing to catch up on, same as the old isFirstTimeUser bypass this replaces.
                var earliestEntry = await _context.TimeTracker
                    .Where(t => t.AzureAdUserId == userId)
                    .Select(t => (DateTime?)t.DateOfEntry)
                    .MinAsync();
                if (earliestEntry == null)
                {
                    return (null, false);
                }

                var lookbackStart = new[] { currentMonday.AddDays(-GapLookbackDays), earliestEntry.Value.Date }.Max();
                var priorDays = await _gapAnalysisService.AnalyzeWeekAsync(userId, lookbackStart, currentMonday.AddDays(-1));
                var blockingDates = priorDays.Where(d => d.RequiresAction).Select(d => d.Date).OrderBy(d => d).ToList();
                if (blockingDates.Any())
                {
                    // A handful of dates are worth naming outright; beyond that, naming every one turns
                    // one message into a wall of dates - the count and range say the same thing more
                    // readably, and the employee sees the exact days either way once they pick a date
                    // to fill in (or on Track Your Time's own gap panel).
                    var dateList = blockingDates.Count <= 5
                        ? string.Join(", ", blockingDates.Select(d => d.ToString("yyyy-MM-dd")))
                        : $"{blockingDates.Count} days between {blockingDates.First():yyyy-MM-dd} and {blockingDates.Last():yyyy-MM-dd}";
                    return ($"You have unresolved timesheet day(s) from a previous week that need attention first - " +
                        $"{dateList}. Go back and fill in (or flag) those days before logging a new week.", false);
                }
                return (null, false);
            }

            if (dateOfEntry < currentMonday.AddDays(-GapLookbackDays) || dateOfEntry > currentFriday)
            {
                return ($"You can only select dates from {currentMonday:yyyy-MM-dd} to {currentFriday:yyyy-MM-dd} (current week), " +
                    "or an earlier date you still need to catch up on.", false);
            }

            var weekOfDate = MondayOf(dateOfEntry);
            var thatWeek = await _gapAnalysisService.AnalyzeWeekAsync(userId, weekOfDate, weekOfDate.AddDays(4));
            var thatDay = thatWeek.FirstOrDefault(d => d.Date.Date == dateOfEntry.Date);
            if (thatDay == null || thatDay.Kind != DayResolutionKind.TrueGap)
            {
                return ($"{dateOfEntry:yyyy-MM-dd} is outside the current week and isn't an outstanding gap - nothing to catch up on there.", false);
            }

            return (null, true);
        }

        /// <summary>Creates or overwrites a Pending gap resolution for (userId, date) and notifies that
        /// employee's manager there's something waiting on them - shared by RequestGapResolution (the
        /// Timesheet Gaps self-service picker) and Create's "flag this day as leave" path, so both entry
        /// points behave identically.</summary>
        private async Task UpsertPendingGapResolutionAsync(
            string userId, DateTime date, GapResolutionMethod method, int? leaveTypeId, string? notes, DateTime weekStart,
            double? hoursOverride = null)
        {
            double resolvedHours;
            if (hoursOverride.HasValue)
            {
                // LateSubmission only: the real hours from the entry the employee just submitted,
                // rather than one of the flat defaults below (which only apply to the other methods,
                // none of which have a real underlying TimeTracker row to read hours from).
                resolvedHours = hoursOverride.Value;
            }
            else if (method == GapResolutionMethod.UnpaidAbsence)
            {
                resolvedHours = 0;
            }
            else if (leaveTypeId.HasValue)
            {
                var leaveType = await _context.LeaveTypes.FindAsync(leaveTypeId.Value);
                resolvedHours = leaveType?.Name == "Unpaid" ? 0 : 8;
            }
            else
            {
                resolvedHours = 8;
            }

            var existing = await _context.GapResolutions
                .Include(g => g.LeaveType)
                .FirstOrDefaultAsync(g => g.AzureAdUserId == userId && g.Date == date.Date);
            if (existing == null)
            {
                existing = new GapResolution { AzureAdUserId = userId, Date = date.Date };
                _context.GapResolutions.Add(existing);
            }
            existing.Method = method;
            existing.LeaveTypeId = leaveTypeId;
            existing.Hours = resolvedHours;
            existing.Notes = notes;
            existing.RequestedByUserId = userId;
            existing.RequestedDate = DateTime.Now;
            existing.Status = GapResolutionStatus.Pending;
            existing.ConfirmedByUserId = null;
            existing.ConfirmedDate = null;
            existing.AutoResolved = false;
            await _context.SaveChangesAsync();

            // Re-fetch the LeaveType nav property so ResolutionLabel below sees the newly-set
            // LeaveTypeId (EF won't auto-populate a nav property changed via a scalar FK assignment
            // above without an explicit reload).
            if (leaveTypeId.HasValue)
            {
                await _context.Entry(existing).Reference(g => g.LeaveType).LoadAsync();
            }

            // Tell the employee's manager there's something waiting on them.
            var employee = await _context.Users.FirstOrDefaultAsync(u => u.AzureAdUserId == userId);
            if (employee != null && !string.IsNullOrWhiteSpace(employee.SupervisorEmail))
            {
                var manager = await _context.Users.FirstOrDefaultAsync(u =>
                    u.Email != null && u.Email.ToLower() == employee.SupervisorEmail.ToLower());
                if (manager != null)
                {
                    await _notificationService.CreateAsync(
                        manager.AzureAdUserId, NotificationType.GapResolutionPendingConfirmation,
                        $"{employee.EmployeeName} {employee.EmployeeSurname} proposed a resolution for {date:yyyy-MM-dd}",
                        $"Method: {WeeklyReportGapAnalysisService.ResolutionLabel(existing)}. Review and confirm or reject it.",
                        $"/Manager/EmployeeTimeSheetDetails?id={userId}&startDate={weekStart:yyyy-MM-dd}&endDate={weekStart.AddDays(4):yyyy-MM-dd}");
                }
            }
        }

        /// <summary>Lets the employee edit the Daily Task text on one of their own past entries. Only
        /// the task changes - Date, Work Location, Start/End Time and hours all stay server-derived from
        /// SharePoint, same rule as at creation time. Editing clears the existing signature: the
        /// employee has to re-type it to confirm the edited task, same as the original submission -
        /// otherwise a corrected entry would carry a signature for text that's since changed.</summary>
        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = User.GetUserId();
            var entry = await _context.TimeTracker.FirstOrDefaultAsync(t => t.TimeTrackerId == id && t.AzureAdUserId == userId);
            if (entry == null)
            {
                return NotFound();
            }
            return View(entry);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, string dailyTask, string signature)
        {
            var userId = User.GetUserId();
            var entry = await _context.TimeTracker.FirstOrDefaultAsync(t => t.TimeTrackerId == id && t.AzureAdUserId == userId);
            if (entry == null)
            {
                return NotFound();
            }

            if (string.IsNullOrWhiteSpace(dailyTask))
            {
                ModelState.AddModelError(nameof(dailyTask), "Daily Task is required.");
            }
            if (string.IsNullOrWhiteSpace(signature))
            {
                ModelState.AddModelError(nameof(signature), "Signature is required to confirm the edited entry.");
            }
            if (!ModelState.IsValid)
            {
                return View(entry);
            }

            entry.DailyTask = dailyTask;
            entry.Signature = signature;

            var (flagged, flagReason) = DetectAnomalies(entry);
            entry.IsFlaggedForReview = flagged;
            entry.FlagReason = flagReason;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"{entry.DateOfEntry:yyyy-MM-dd} updated and re-signed.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> GenerateWeeklyReport(DateTime startDate, DateTime endDate)
        {
            // Fails loudly instead of silently generating a garbage PDF (0001-01-01, zero entries,
            // blank signature) - the previous version had no guard at all here, so any client-side
            // hiccup (empty date field, JS error before the fields were read) went straight through.
            if (startDate == default || endDate == default)
            {
                TempData["ErrorMessage"] = "Couldn't generate the report: no valid date range was received. " +
                    "Please filter to a complete Monday-Friday week first, then try again.";
                return RedirectToAction(nameof(Index));
            }

            if (endDate < startDate)
            {
                TempData["ErrorMessage"] = "Couldn't generate the report: the end date is before the start date.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var userId = User.GetUserId();

                var timeEntries = await _context.TimeTracker
                    .Where(t => t.AzureAdUserId == userId &&
                                t.DateOfEntry >= startDate &&
                                t.DateOfEntry <= endDate)
                    .OrderBy(t => t.DateOfEntry)
                    .ToListAsync();

                if (!timeEntries.Any())
                {
                    TempData["ErrorMessage"] = $"No timesheet entries were found between {startDate:yyyy-MM-dd} " +
                        $"and {endDate:yyyy-MM-dd} - nothing to include in the report.";
                    return RedirectToAction(nameof(Index));
                }

                // Every daily entry is signed at submission time now (see Signature on TimeTrackerModel,
                // captured by TimeTrackerController.Create) - that's the single source of truth for the
                // Employee Signature on the weekly PDF, not a separate typed-at-export-time signature.
                var unsignedDates = timeEntries.Where(t => string.IsNullOrWhiteSpace(t.Signature))
                    .Select(t => t.DateOfEntry.ToString("yyyy-MM-dd"))
                    .ToList();
                if (unsignedDates.Any())
                {
                    TempData["ErrorMessage"] = "Couldn't generate the report: the following day(s) in this week " +
                        $"aren't signed yet - {string.Join(", ", unsignedDates)}. Each daily timesheet needs its " +
                        "own signature before a weekly report can be generated.";
                    return RedirectToAction(nameof(Index));
                }

                var employee = await _context.TimeTracker
                    .Where(t => t.AzureAdUserId == userId)
                    .Select(t => new { t.EmployeeName, t.EmployeeSurname, t.JobTitle, t.SupervisorFullName })
                    .FirstOrDefaultAsync();

                if (employee == null)
                {
                    return NotFound();
                }

                // The most recent signed day in the week stands for the whole week's signature - in
                // practice every day carries the same typed name anyway.
                var lastEntry = timeEntries[^1];

                // A manager may have already counter-signed this week (ManagerController.
                // GenerateEmployeeWeeklyReport) - pull that in too so the employee's own copy shows both
                // signatures once both sides have signed, not just their own.
                var signatureRow = await _context.WeeklyTimesheetSignatures.FirstOrDefaultAsync(s =>
                    s.AzureAdUserId == userId && s.WeekStartDate == startDate && s.WeekEndDate == endDate);

                // No gap analysis on this side (leave/holiday/gap resolution is a manager-only concern,
                // via ManagerController.GenerateEmployeeWeeklyReport) - just the employee's own signed
                // entries, one row each, same as before.
                var dayRows = timeEntries.Select(entry => new DayResolution
                {
                    Date = entry.DateOfEntry,
                    Kind = DayResolutionKind.Worked,
                    TaskDescription = entry.DailyTask ?? "",
                    StartTime = entry.StartTime,
                    EndTime = entry.EndTime,
                    Hours = entry.TotalHrsWorked,
                    WorkLocation = entry.WorkLocation,
                    SignatureDisplay = entry.Signature,
                    SourceEntry = entry
                }).ToList();

                var pdfBytes = _pdfService.GenerateWeeklyReport(new WeeklyReportPdfRequest
                {
                    EmployeeName = employee.EmployeeName,
                    EmployeeSurname = employee.EmployeeSurname,
                    JobTitle = employee.JobTitle,
                    SupervisorFullName = employee.SupervisorFullName,
                    StartDate = startDate,
                    EndDate = endDate,
                    DayRows = dayRows,
                    Signature = lastEntry.Signature,
                    SignatureDate = lastEntry.DateOfEntry,
                    SupervisorSignature = signatureRow?.SupervisorSignature ?? "",
                    SupervisorSignatureDate = signatureRow?.SupervisorSignatureDate
                });

                return File(pdfBytes, "application/pdf",
                    $"{employee.EmployeeName}_{employee.EmployeeSurname}_Timesheet_{startDate:yyyyMMdd}_to_{endDate:yyyyMMdd}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating weekly report PDF");
                throw;
            }
        }

        // Rule-based anomaly detection (roadmap §7.2): deterministic range checks, not an AI/ML model.
        // Flags an entry for manager review without blocking the save - the employee can still submit,
        // but the flag + reason travel with the row for the Manager Board / timesheet details screens.
        private static (bool Flagged, string? Reason) DetectAnomalies(TimeTrackerModel entry)
        {
            if (entry.IsPublicHoliday)
            {
                return (false, null);
            }

            var reasons = new List<string>();

            if (entry.StartTime.TimeOfDay < TimeSpan.FromHours(5))
            {
                reasons.Add($"Start time ({entry.StartTime:HH:mm}) is before 5:00 AM.");
            }

            var shiftLength = entry.EndTime.TimeOfDay - entry.StartTime.TimeOfDay;
            if (shiftLength > TimeSpan.FromHours(12))
            {
                reasons.Add($"Shift is longer than 12 hours ({FormatHoursStatic(shiftLength.TotalHours)}).");
            }

            return reasons.Count > 0 ? (true, string.Join(" ", reasons)) : (false, null);
        }

        private static string FormatHoursStatic(double totalHours)
        {
            int hours = (int)totalHours;
            int minutes = (int)Math.Round((totalHours - hours) * 60);
            return $"{hours}h{minutes:D2}m";
        }

        // Keeps UserAccount.SupervisorFullName/SupervisorEmail in step with SharePoint's "Reports to"
        // column, so the Manager Board (which queries by SupervisorEmail - see ManagerController)
        // reflects the SharePoint reporting line without needing its own query layer rewritten.
        //
        // "Reports to" only gives a name ("Peter Bereta | Providence Software ZA", not an email), so
        // the email is resolved best-effort by matching that name against an existing UserAccount. If
        // the manager isn't seeded/provisioned in TymSheet yet, SupervisorFullName still updates (so it
        // displays correctly) but SupervisorEmail is left as-is - until that manager has an account
        // here, they won't see this employee's timesheets on their Manager Board. Seed/provision
        // managers as they appear in "Reports to" to close that gap.
        private async Task SyncManagerFromSharePointAsync(PSS_Time_Tracker.Models.UserAccount userAccount, SharePointCheckInRecord? checkIn)
        {
            if (checkIn?.ReportsToName is not { Length: > 0 } reportsToName ||
                string.Equals(userAccount.SupervisorFullName, reportsToName, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            userAccount.SupervisorFullName = reportsToName;

            var (managerGivenNames, managerSurname) = SplitFullName(reportsToName);
            if (!string.IsNullOrEmpty(managerSurname))
            {
                var manager = await _context.Users.FirstOrDefaultAsync(u =>
                    u.EmployeeName.ToLower() == managerGivenNames.ToLower() &&
                    u.EmployeeSurname.ToLower() == managerSurname.ToLower());

                if (manager != null)
                {
                    userAccount.SupervisorEmail = manager.Email;
                }
                else
                {
                    _logger.LogWarning("Reports-to manager '{Name}' has no matching UserAccount yet - " +
                        "Manager Board visibility for {Employee} depends on that account existing.",
                        reportsToName, userAccount.Email);
                }
            }

            await _context.SaveChangesAsync();
        }

    }
}