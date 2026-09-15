using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PSS_Time_Tracker.Data;
using PSS_Time_Tracker.Models;
using PSS_Time_Tracker.Services;

namespace PSS_Time_Tracker.Controllers
{
    /// <summary>
    /// Line-manager side of leave approval: "Approve Time Off". Handles only the first sign-off stage
    /// (Reporting Manager recommendation) from the real Employee Leave Application form, and only for
    /// this manager's own reports - matched via UserAccount.SupervisorEmail, same as the existing
    /// Manager Board (see ManagerController). Once recommended, a request moves to the HR stage,
    /// handled separately by <see cref="HrApprovalController"/> under its own RequireHrRole policy -
    /// a manager who isn't also HR has no visibility into that stage.
    /// </summary>
    [Authorize(Policy = "RequireManagerRole")]
    public class LeaveApprovalController : Controller
    {
        private readonly timeSheetRecorderContext _context;
        private readonly EmailService _emailService;
        private readonly ILogger<LeaveApprovalController> _logger;
        private readonly INotificationService _notificationService;

        public LeaveApprovalController(
            timeSheetRecorderContext context, EmailService emailService, ILogger<LeaveApprovalController> logger,
            INotificationService notificationService)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
            _notificationService = notificationService;
        }

        public async Task<IActionResult> Index(int page = 1)
        {
            const int pageSize = 10;

            var managerEmail = User.FindFirst("preferred_username")?.Value ?? User.FindFirst("email")?.Value;

            // Only this manager's own reports - matches how ManagerController.Index scopes the
            // Manager Board, so a manager never sees another team's leave requests here.
            var myReportIds = await _context.Users
                .Where(u => u.SupervisorEmail == managerEmail)
                .Select(u => u.AzureAdUserId)
                .ToListAsync();

            var query = _context.LeaveRequests
                .Include(lr => lr.LeaveType)
                .Where(lr => lr.Status == LeaveRequestStatus.PendingManager && myReportIds.Contains(lr.AzureAdUserId))
                .OrderBy(lr => lr.CreatedDate);

            var totalRecords = await query.CountAsync();
            var requests = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            // Employee display names aren't stored on LeaveRequest itself (it only holds AzureAdUserId),
            // so look them up in one pass rather than N+1 queries per row.
            var userIds = requests.Select(r => r.AzureAdUserId).Distinct().ToList();
            ViewBag.Employees = await _context.Users
                .Where(u => userIds.Contains(u.AzureAdUserId))
                .ToDictionaryAsync(u => u.AzureAdUserId, u => $"{u.EmployeeName} {u.EmployeeSurname}");

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
            ViewBag.HasPrevious = page > 1;
            ViewBag.HasNext = page < ViewBag.TotalPages;

            return View(requests);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManagerDecision(
            int id, ManagerRecommendation recommendation, bool? approvedPaidLeave, string? remarks, string signature)
        {
            var managerEmail = User.FindFirst("preferred_username")?.Value ?? User.FindFirst("email")?.Value;

            var leaveRequest = await _context.LeaveRequests.FirstOrDefaultAsync(lr => lr.Id == id);
            if (leaveRequest == null || leaveRequest.Status != LeaveRequestStatus.PendingManager)
            {
                return NotFound();
            }

            // Belt-and-braces: the Index query already scopes to this manager's reports, but this
            // stops a manager acting on someone else's request via a hand-crafted POST too.
            var employee = await _context.Users.FirstOrDefaultAsync(u => u.AzureAdUserId == leaveRequest.AzureAdUserId);
            if (employee == null || !string.Equals(employee.SupervisorEmail, managerEmail, StringComparison.OrdinalIgnoreCase))
            {
                return Forbid();
            }

            leaveRequest.ManagerRecommendation = recommendation;
            leaveRequest.ManagerApprovedPaidLeave = approvedPaidLeave;
            leaveRequest.ManagerRemarks = remarks;
            leaveRequest.ManagerSignature = signature;
            leaveRequest.ManagerDecisionDate = DateTime.Now;
            leaveRequest.ManagerUserId = User.GetUserId();

            // "Recommended" is the only outcome that advances the request; "Not Recommended" and
            // "Reschedule Request" both end it here, matching the paper form's single Manager sign-off
            // block - the employee resubmits fresh dates rather than editing this request in place.
            leaveRequest.Status = recommendation == ManagerRecommendation.Recommended
                ? LeaveRequestStatus.PendingHR
                : LeaveRequestStatus.RejectedByManager;

            await _context.SaveChangesAsync();
            await NotifyEmployeeAsync(leaveRequest, "Reporting Manager", $"Recommendation: {recommendation}." +
                (string.IsNullOrWhiteSpace(remarks) ? "" : $" Remarks: {remarks}"));

            // Recommended requests move on to HR - notify every HR user (HR access isn't scoped to a
            // reporting line, so every HR account gets pinged, not just one).
            if (leaveRequest.Status == LeaveRequestStatus.PendingHR)
            {
                await NotifyHrAsync(leaveRequest, employee);
            }

            TempData["SuccessMessage"] = "Manager recommendation recorded.";
            return RedirectToAction(nameof(Index));
        }

        private async Task NotifyEmployeeAsync(LeaveRequest leaveRequest, string stageName, string summary)
        {
            var employee = await _context.Users.FirstOrDefaultAsync(u => u.AzureAdUserId == leaveRequest.AzureAdUserId);
            if (employee == null)
            {
                return;
            }

            // In-app "bell" notification, alongside the email below - not gated on having an email
            // address, same as NotifyHrAsync's. Previously this method only ever sent email, so an
            // employee had no way at all to learn their leave request was decided short of manually
            // re-checking Leave/Index - confirmed live during QA (approving/rejecting as manager left
            // zero rows in Notifications for the employee either way).
            await _notificationService.CreateAsync(
                employee.AzureAdUserId, NotificationType.LeaveRequestDecision,
                $"{stageName} decision on your leave request",
                summary,
                "/Leave/Index");

            if (string.IsNullOrWhiteSpace(employee.Email))
            {
                return;
            }

            try
            {
                await _emailService.SendLeaveDecisionEmailAsync(
                    employee.Email,
                    $"{employee.EmployeeName} {employee.EmployeeSurname}",
                    stageName,
                    summary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send leave decision notification email");
            }
        }

        private async Task NotifyHrAsync(LeaveRequest leaveRequest, UserAccount employee)
        {
            var hrUsers = await _context.Users.Where(u => u.IsHr).ToListAsync();
            var leaveTypeName = leaveRequest.LeaveType?.Name
                ?? (await _context.LeaveTypes.FindAsync(leaveRequest.LeaveTypeId))?.Name
                ?? "Leave";

            // In-app "bell" notifications for every HR user, alongside the emails below - see
            // INotificationService. Not gated on having an email address, unlike the email loop, since
            // this channel doesn't need one.
            await _notificationService.CreateForManyAsync(
                hrUsers.Select(u => u.AzureAdUserId), NotificationType.LeaveRequestSubmitted,
                $"{employee.EmployeeName} {employee.EmployeeSurname} needs an HR decision on {leaveTypeName} leave",
                $"{leaveRequest.StartDate:yyyy-MM-dd} to {leaveRequest.EndDate:yyyy-MM-dd}. Review it on the HR Board.",
                "/HrApproval/Index");

            foreach (var hrUser in hrUsers)
            {
                if (string.IsNullOrWhiteSpace(hrUser.Email))
                {
                    continue;
                }

                try
                {
                    await _emailService.SendLeaveRequestToHrAsync(
                        hrUser.Email,
                        $"{hrUser.EmployeeName} {hrUser.EmployeeSurname}",
                        $"{employee.EmployeeName} {employee.EmployeeSurname}",
                        leaveTypeName,
                        leaveRequest.StartDate,
                        leaveRequest.EndDate);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send leave request notification to HR user {HrEmail}", hrUser.Email);
                }
            }
        }
    }
}
