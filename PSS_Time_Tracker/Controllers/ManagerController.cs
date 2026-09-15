using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using PSS_Time_Tracker;
using PSS_Time_Tracker.Data;
using PSS_Time_Tracker.Models;
using PSS_Time_Tracker.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;


namespace PSS_Time_Tracker.Controllers
{
    // Everything here deals with one specific manager's own reports' timesheets - every action that
    // takes an employeeId/id re-checks IsManagedByCurrentUserAsync below, on top of this policy, so a
    // manager can't view or act on another manager's team by hand-crafting a URL with someone else's id.
    [Authorize(Policy = "RequireManagerRole")]
    public class ManagerController : Controller
    {
        private readonly timeSheetRecorderContext _context;
        private readonly ITimesheetPdfService _pdfService;
        private readonly IWeeklyReportGapAnalysisService _gapAnalysisService;
        private readonly INotificationService _notificationService;
        private readonly EmailService _emailService;

        public ManagerController(
            timeSheetRecorderContext context,
            ITimesheetPdfService pdfService,
            IWeeklyReportGapAnalysisService gapAnalysisService,
            INotificationService notificationService,
            EmailService emailService)
        {
            _context = context;
            _pdfService = pdfService;
            _gapAnalysisService = gapAnalysisService;
            _notificationService = notificationService;
            _emailService = emailService;
        }

        private string? CurrentManagerEmail =>
            User.FindFirst("preferred_username")?.Value ?? User.FindFirst("email")?.Value;

        /// <summary>Same Monday-of-week formula used in TimeTrackerController - for building a
        /// deep-linking Track Your Time URL from a single date.</summary>
        private static DateTime MondayOfWeek(DateTime date)
        {
            var mondayOffset = date.DayOfWeek == DayOfWeek.Sunday ? -6 : (int)DayOfWeek.Monday - (int)date.DayOfWeek;
            return date.Date.AddDays(mondayOffset);
        }

        /// <summary>Only this employee's own reporting manager may view or act on their timesheet -
        /// matches how the Manager Board list (Index, below) is already scoped by SupervisorEmail.</summary>
        private async Task<bool> IsManagedByCurrentUserAsync(string employeeId)
        {
            if (string.IsNullOrEmpty(employeeId))
            {
                return false;
            }
            var managerEmail = CurrentManagerEmail;
            return await _context.Users.AnyAsync(u =>
                u.AzureAdUserId == employeeId &&
                u.SupervisorEmail != null &&
                u.SupervisorEmail.ToLower() == managerEmail!.ToLower());
        }

        public async Task<IActionResult> Index(int page = 1, string searchTerm = null)
        {

            int pageSize = 10;

            var managerEmail = CurrentManagerEmail;
            var searchParam = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm;

            List<EmployeeSummaryResult> employeeGroups;
            int totalRecords;

            employeeGroups = await _context.EmployeeSummaryResults
                .FromSqlInterpolated($@"
        EXEC GetManagerEmployeeTimeSheetsPaged
            @SupervisorEmail={managerEmail},
            @StartDate={null},
            @EndDate={null},
            @SearchTerm={searchParam},
            @PageNumber={page},
            @PageSize={pageSize}")
                .AsNoTracking()
                .ToListAsync();

            totalRecords = _context.SpResults
                .FromSqlInterpolated($@"
        EXEC GetManagerEmployeeTimeSheetsCount
            @SupervisorEmail={managerEmail},
            @StartDate={null},
            @EndDate={null},
            @SearchTerm={searchParam}")
                .AsNoTracking()
                .AsEnumerable()
                .Select(r => r.TotalCount)
                .FirstOrDefault();




            var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

            var employees = employeeGroups.Select(g => new EmployeeViewModel
            {
                AzureAdUserId = g.AzureAdUserId,
                FullName = $"{g.EmployeeName} {g.EmployeeSurname}",
                TimesheetCount = g.TimesheetCount,
                LatestEntry = g.LatestEntry
            }).ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.HasPrevious = page > 1;
            ViewBag.HasNext = page < totalPages;
            ViewBag.SearchTerm = searchTerm;

            return View(employees);
        }



        public async Task<IActionResult> EmployeeTimeSheetDetails(string id, DateTime? startDate, DateTime? endDate, int page = 1)
        {
            int pageSize = 10;

            if (string.IsNullOrEmpty(id))
            {
                return NotFound();
            }

            if (!await IsManagedByCurrentUserAsync(id))
            {
                return Forbid();
            }

            var employeeIdParam = new SqlParameter("@EmployeeId", id);
            var startDateParam = new SqlParameter("@StartDate", (object?)startDate ?? DBNull.Value);
            var endDateParam = new SqlParameter("@EndDate", (object?)endDate ?? DBNull.Value);
            var pageNumberParam = new SqlParameter("@PageNumber", page);
            var pageSizeParam = new SqlParameter("@PageSize", pageSize);


            var timesheetEntries = await _context.TimeTracker
                .FromSqlRaw("EXEC GetEmployeeTimesheetPaged @EmployeeId, @StartDate, @EndDate, @PageNumber, @PageSize",
                    employeeIdParam, startDateParam, endDateParam, pageNumberParam, pageSizeParam)
                .ToListAsync();


            var totalEntries = await _context.Database.ExecuteSqlInterpolatedAsync(
                $@"EXEC GetEmployeeTimesheetCount @EmployeeId={id}, @StartDate={startDate}, @EndDate={endDate}");

            var totalPages = (int)Math.Ceiling(totalEntries / (double)pageSize);
            var hasPrevious = page > 1;
            var hasNext = page < totalPages;


            // Looked up from Users (by the id already validated above via IsManagedByCurrentUserAsync)
            // rather than inferred from the first paged TimeTracker row - an employee whose whole
            // range is gap resolutions/leave, with zero raw TimeTracker rows, previously showed as
            // "Timesheet Details for Unknown" even though their identity was never actually in doubt.
            var employeeAccount = await _context.Users.FirstOrDefaultAsync(u => u.AzureAdUserId == id);
            var fullName = employeeAccount != null
                ? $"{employeeAccount.EmployeeName} {employeeAccount.EmployeeSurname}"
                : "Unknown";


            ViewBag.EmployeeId = id;
            ViewBag.EmployeeName = fullName;
            ViewBag.StartDate = startDate?.ToString("yyyy-MM-dd");
            ViewBag.EndDate = endDate?.ToString("yyyy-MM-dd");
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.HasPrevious = hasPrevious;
            ViewBag.HasNext = hasNext;
            ViewBag.ShowReportButton = timesheetEntries.Any();

            // Only meaningful once the manager has filtered to a specific week - drives the "Timesheet
            // Gaps" panel and the Generate Report button's Strict-mode gate on the view.
            if (startDate.HasValue && endDate.HasValue)
            {
                ViewBag.DayResolutions = await _gapAnalysisService.AnalyzeWeekAsync(id, startDate.Value, endDate.Value);
            }

            return View(timesheetEntries);
        }

        /// <summary>Saves (or overwrites) how a manager chose to resolve one gap date for an employee's
        /// week - a true gap, or an "Awaiting Signature" day the manager wants to bypass (a real entry
        /// exists but the employee hasn't signed it) - then returns to the same filtered Employee
        /// Timesheet Details view. A manager's own resolution is self-confirmed immediately (no
        /// approval loop needed, unlike an employee's self-service proposal via
        /// TimeTrackerController.RequestGapResolution).</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResolveGap(
            string employeeId, DateTime date, GapResolutionMethod method, double? hours, string? notes,
            DateTime? startDate, DateTime? endDate)
        {
            if (string.IsNullOrEmpty(employeeId) || date == default)
            {
                return BadRequest();
            }

            if (!await IsManagedByCurrentUserAsync(employeeId))
            {
                return Forbid();
            }

            double resolvedHours = method switch
            {
                GapResolutionMethod.RetroactiveLeave => 8,
                GapResolutionMethod.Holiday => 8,
                GapResolutionMethod.ManualPunch => hours.GetValueOrDefault(8),
                _ => 0
            };

            var managerId = User.GetUserId() ?? "";
            var existing = await _context.GapResolutions.FirstOrDefaultAsync(g =>
                g.AzureAdUserId == employeeId && g.Date == date.Date);
            if (existing == null)
            {
                existing = new GapResolution { AzureAdUserId = employeeId, Date = date.Date };
                _context.GapResolutions.Add(existing);
            }
            existing.Method = method;
            existing.Hours = resolvedHours;
            existing.Notes = notes;
            existing.RequestedByUserId = managerId;
            existing.RequestedDate = DateTime.Now;
            existing.Status = GapResolutionStatus.Confirmed;
            existing.ConfirmedByUserId = managerId;
            existing.ConfirmedDate = DateTime.Now;
            existing.AutoResolved = false;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"{date:yyyy-MM-dd} resolved.";
            return RedirectToAction(nameof(EmployeeTimeSheetDetails), new { id = employeeId, startDate, endDate });
        }

        /// <summary>Manager confirms or rejects a gap resolution an employee proposed for their own
        /// timesheet (TimeTrackerController.RequestGapResolution). Rejecting leaves the date open for a
        /// fresh proposal (from either side) rather than deleting the record, so there's an audit trail.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmGapResolution(
            int gapResolutionId, bool approve, DateTime? startDate, DateTime? endDate)
        {
            var resolution = await _context.GapResolutions.Include(g => g.LeaveType).FirstOrDefaultAsync(g => g.Id == gapResolutionId);
            if (resolution == null)
            {
                return NotFound();
            }

            if (!await IsManagedByCurrentUserAsync(resolution.AzureAdUserId))
            {
                return Forbid();
            }

            resolution.Status = approve ? GapResolutionStatus.Confirmed : GapResolutionStatus.Rejected;
            resolution.ConfirmedByUserId = User.GetUserId();
            resolution.ConfirmedDate = DateTime.Now;
            await _context.SaveChangesAsync();

            var employee = await _context.Users.FirstOrDefaultAsync(u => u.AzureAdUserId == resolution.AzureAdUserId);
            if (employee != null)
            {
                var title = approve
                    ? $"Your {resolution.Date:yyyy-MM-dd} timesheet gap was confirmed"
                    : $"Your {resolution.Date:yyyy-MM-dd} timesheet gap request was rejected";
                var weekStart = MondayOfWeek(resolution.Date);
                await _notificationService.CreateAsync(
                    resolution.AzureAdUserId, NotificationType.GapResolutionPendingConfirmation, title,
                    approve
                        ? $"Your manager confirmed your {WeeklyReportGapAnalysisService.ResolutionLabel(resolution)} request."
                        : "Your manager rejected this request - please check with them or submit a different resolution.",
                    $"/TimeTracker/Index?startDate={weekStart:yyyy-MM-dd}&endDate={weekStart.AddDays(4):yyyy-MM-dd}");
            }

            TempData["SuccessMessage"] = approve
                ? $"{resolution.Date:yyyy-MM-dd} confirmed."
                : $"{resolution.Date:yyyy-MM-dd} rejected.";
            return RedirectToAction(nameof(EmployeeTimeSheetDetails), new { id = resolution.AzureAdUserId, startDate, endDate });
        }

        /// <summary>Lets a manager proactively tell an employee their week has unresolved timesheet
        /// gaps - an in-app notification (and, when SMTP is configured, an email) rather than the
        /// employee having to notice on their own.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> NotifyEmployeeOfGaps(string employeeId, DateTime startDate, DateTime endDate)
        {
            if (!await IsManagedByCurrentUserAsync(employeeId))
            {
                return Forbid();
            }

            var dayResolutions = await _gapAnalysisService.AnalyzeWeekAsync(employeeId, startDate, endDate);
            var actionableDates = dayResolutions.Where(d => d.RequiresAction).Select(d => d.Date.ToString("yyyy-MM-dd")).ToList();

            if (actionableDates.Any())
            {
                var employee = await _context.Users.FirstOrDefaultAsync(u => u.AzureAdUserId == employeeId);
                var message = $"Your timesheet for {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd} has unresolved " +
                    $"day(s): {string.Join(", ", actionableDates)}. Please review on Track Your Time.";

                await _notificationService.CreateAsync(
                    employeeId, NotificationType.TimesheetGapFlagged, "Timesheet gaps need your attention", message,
                    $"/TimeTracker/Index?startDate={startDate:yyyy-MM-dd}&endDate={endDate:yyyy-MM-dd}");

                if (employee != null && !string.IsNullOrWhiteSpace(employee.Email))
                {
                    try
                    {
                        await _emailService.SendTimesheetGapNotificationAsync(
                            employee.Email, $"{employee.EmployeeName} {employee.EmployeeSurname}", message);
                    }
                    catch
                    {
                        // Email is best-effort here, same as every other notification path in this app -
                        // the in-app notification above is what actually guarantees delivery.
                    }
                }
            }

            TempData["SuccessMessage"] = actionableDates.Any()
                ? "Employee notified about their timesheet gaps."
                : "No unresolved gaps for this week - nothing to notify.";
            return RedirectToAction(nameof(EmployeeTimeSheetDetails), new { id = employeeId, startDate, endDate });
        }

        /// <summary>Alternative to resolving a gap or an unsigned entry directly (ResolveGap): rather
        /// than the manager deciding what happened on that date themselves, this asks the employee to go
        /// fill in (or re-sign) their own timesheet for it, with an optional reason - "please submit
        /// Sept 10, looks like you missed it." Doesn't touch the gap's status at all - it stays open
        /// until the employee actually submits something, same as before this was clicked.</summary>
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestTimesheetFromEmployee(
            string employeeId, DateTime date, string? reason, DateTime? startDate, DateTime? endDate)
        {
            if (string.IsNullOrEmpty(employeeId) || date == default)
            {
                return BadRequest();
            }

            if (!await IsManagedByCurrentUserAsync(employeeId))
            {
                return Forbid();
            }

            var employee = await _context.Users.FirstOrDefaultAsync(u => u.AzureAdUserId == employeeId);
            var message = string.IsNullOrWhiteSpace(reason)
                ? $"Your manager is requesting your timesheet for {date:yyyy-MM-dd}. Please fill it in and sign it on Capture Your Timesheet."
                : $"Your manager is requesting your timesheet for {date:yyyy-MM-dd}. Reason: {reason}";

            await _notificationService.CreateAsync(
                employeeId, NotificationType.TimesheetGapFlagged, $"Timesheet requested for {date:yyyy-MM-dd}", message,
                $"/TimeTracker/Create?date={date:yyyy-MM-dd}");

            if (employee != null && !string.IsNullOrWhiteSpace(employee.Email))
            {
                try
                {
                    await _emailService.SendTimesheetGapNotificationAsync(
                        employee.Email, $"{employee.EmployeeName} {employee.EmployeeSurname}", message);
                }
                catch
                {
                    // Email is best-effort here, same as every other notification path in this app.
                }
            }

            TempData["SuccessMessage"] = $"Requested {date:yyyy-MM-dd}'s timesheet from the employee.";
            return RedirectToAction(nameof(EmployeeTimeSheetDetails), new { id = employeeId, startDate, endDate });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult>
        GenerateEmployeeWeeklyReport(
            DateTime startDate, DateTime endDate, string employeeId, string? supervisorSignature, string? gapResolutionMode)
        {
            // Same guard as TimeTrackerController.GenerateWeeklyReport - fails loudly instead of
            // silently generating a garbage PDF (0001-01-01, zero entries) if the client ever sends a
            // missing/default date range.
            if (startDate == default || endDate == default || string.IsNullOrEmpty(employeeId))
            {
                TempData["ErrorMessage"] = "Couldn't generate the report: no valid date range was received. " +
                    "Please filter to a complete Monday-Friday week first, then try again.";
                return RedirectToAction(nameof(EmployeeTimeSheetDetails), new { id = employeeId, startDate, endDate });
            }

            if (!await IsManagedByCurrentUserAsync(employeeId))
            {
                return Forbid();
            }

            if (endDate < startDate)
            {
                TempData["ErrorMessage"] = "Couldn't generate the report: the end date is before the start date.";
                return RedirectToAction(nameof(EmployeeTimeSheetDetails), new { id = employeeId, startDate, endDate });
            }

            if (string.IsNullOrWhiteSpace(supervisorSignature))
            {
                TempData["ErrorMessage"] = "Couldn't generate the report: a supervisor signature is required.";
                return RedirectToAction(nameof(EmployeeTimeSheetDetails), new { id = employeeId, startDate, endDate });
            }

            // Strict is the safer default for a payroll document - a report only gets auto-resolved
            // gaps (0 hrs Unpaid Absence) when the manager explicitly opts into Soft mode on the screen.
            bool strictMode = !string.Equals(gapResolutionMode, "Soft", StringComparison.OrdinalIgnoreCase);

            try
            {
                var employee = await _context.Users.FirstOrDefaultAsync(u => u.AzureAdUserId == employeeId);
                if (employee == null)
                {
                    return NotFound();
                }

                var dayResolutions = await _gapAnalysisService.AnalyzeWeekAsync(employeeId, startDate, endDate);

                // A real entry that hasn't been signed yet always blocks, in either mode, unless a
                // manager already bypassed it via ResolveGap (which reclassifies the date as
                // GapResolved before it ever reaches this check).
                var needsSignatureDates = dayResolutions
                    .Where(d => d.Kind == DayResolutionKind.NeedsSignature)
                    .Select(d => d.Date.ToString("yyyy-MM-dd"))
                    .ToList();
                if (needsSignatureDates.Any())
                {
                    TempData["ErrorMessage"] = "Couldn't generate the report: this employee hasn't signed the " +
                        $"following day(s) yet - {string.Join(", ", needsSignatureDates)}. Ask them to sign each " +
                        "day's timesheet, or resolve/bypass it below, then try again.";
                    return RedirectToAction(nameof(EmployeeTimeSheetDetails), new { id = employeeId, startDate, endDate });
                }

                // An employee-proposed resolution still awaiting this manager's confirm/reject always
                // blocks too, in either mode - see DayResolutionKind.PendingConfirmation.
                var pendingDates = dayResolutions
                    .Where(d => d.Kind == DayResolutionKind.PendingConfirmation)
                    .Select(d => d.Date.ToString("yyyy-MM-dd"))
                    .ToList();
                if (pendingDates.Any())
                {
                    TempData["ErrorMessage"] = "Couldn't generate the report: the following day(s) have a " +
                        $"resolution the employee proposed, awaiting your confirmation - {string.Join(", ", pendingDates)}. " +
                        "Confirm or reject each one in the Timesheet Gaps panel below, then try again.";
                    return RedirectToAction(nameof(EmployeeTimeSheetDetails), new { id = employeeId, startDate, endDate });
                }

                var trueGaps = dayResolutions.Where(d => d.Kind == DayResolutionKind.TrueGap).ToList();
                if (trueGaps.Any())
                {
                    if (strictMode)
                    {
                        var gapDates = string.Join(", ", trueGaps.Select(g => g.Date.ToString("yyyy-MM-dd")));
                        TempData["ErrorMessage"] = "Couldn't generate the report: the following day(s) have no " +
                            $"clock-in, approved leave, or public holiday - {gapDates}. Resolve each one in the " +
                            "Timesheet Gaps panel below (or switch to Soft mode), then try again.";
                        return RedirectToAction(nameof(EmployeeTimeSheetDetails), new { id = employeeId, startDate, endDate });
                    }

                    // Soft mode: auto-resolve every true gap as Unpaid Absence / 0 hours, persisted for
                    // audit and so the report reflects a flagged status rather than just vanishing the day.
                    var managerId = User.GetUserId() ?? "";
                    foreach (var gap in trueGaps)
                    {
                        _context.GapResolutions.Add(new GapResolution
                        {
                            AzureAdUserId = employeeId,
                            Date = gap.Date,
                            Method = GapResolutionMethod.UnpaidAbsence,
                            Hours = 0,
                            RequestedByUserId = managerId,
                            Status = GapResolutionStatus.Confirmed,
                            ConfirmedByUserId = managerId,
                            ConfirmedDate = DateTime.Now,
                            AutoResolved = true
                        });
                    }
                    await _context.SaveChangesAsync();
                    dayResolutions = await _gapAnalysisService.AnalyzeWeekAsync(employeeId, startDate, endDate);
                }

                // The most recent worked/signed day in the week stands for the whole week's Employee
                // Signature block - in practice every worked day carries the same typed name anyway.
                var lastWorkedRow = dayResolutions.LastOrDefault(d => d.Kind == DayResolutionKind.Worked);

                // Record this manager's signature for this week, keyed to the employee, so their own copy
                // (TimeTrackerController.GenerateWeeklyReport) picks it up too if they regenerate later.
                var signatureRow = await _context.WeeklyTimesheetSignatures.FirstOrDefaultAsync(s =>
                    s.AzureAdUserId == employeeId && s.WeekStartDate == startDate && s.WeekEndDate == endDate);
                if (signatureRow == null)
                {
                    signatureRow = new WeeklyTimesheetSignature
                    {
                        AzureAdUserId = employeeId,
                        WeekStartDate = startDate,
                        WeekEndDate = endDate
                    };
                    _context.WeeklyTimesheetSignatures.Add(signatureRow);
                }
                signatureRow.SupervisorSignature = supervisorSignature;
                signatureRow.SupervisorSignatureDate = DateTime.Now;
                await _context.SaveChangesAsync();

                var pdfBytes = _pdfService.GenerateWeeklyReport(new WeeklyReportPdfRequest
                {
                    EmployeeName = employee.EmployeeName,
                    EmployeeSurname = employee.EmployeeSurname,
                    JobTitle = employee.JobTitle,
                    SupervisorFullName = employee.SupervisorFullName,
                    StartDate = startDate,
                    EndDate = endDate,
                    DayRows = dayResolutions,
                    Signature = lastWorkedRow?.SignatureDisplay ?? "",
                    SignatureDate = lastWorkedRow?.Date,
                    SupervisorSignature = signatureRow.SupervisorSignature ?? "",
                    SupervisorSignatureDate = signatureRow.SupervisorSignatureDate
                });

                return File(pdfBytes, "application/pdf",
                    $"{employee.EmployeeName}_{employee.EmployeeSurname}_Timesheet_{startDate:yyyyMMdd}_to_{endDate:yyyyMMdd}.pdf");
            }
            catch (Exception)
            {
                return StatusCode(500, "An error occurred while generating the report.");
            }
        }

    }
}
