using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PSS_Time_Tracker.Data;
using PSS_Time_Tracker.Models;

namespace PSS_Time_Tracker.Controllers
{
    /// <summary>
    /// HR side of leave approval: "HR Board". Handles the two later sign-off stages from the real
    /// Employee Leave Application form - HR Manager decision (with pay / without pay / not approved)
    /// and Administration/Payroll capture (balance deduction) - once a request has already been
    /// recommended by the employee's line manager (see <see cref="LeaveApprovalController"/>).
    /// Gated by RequireHrRole (UserAccount.IsHr), a separate flag from IsManager - a line manager who
    /// isn't also HR has no access here, and HR sees every request at this stage regardless of which
    /// manager it came from (HR access isn't scoped to a reporting line the way a manager's is).
    /// </summary>
    [Authorize(Policy = "RequireHrRole")]
    public class HrApprovalController : Controller
    {
        private readonly timeSheetRecorderContext _context;
        private readonly EmailService _emailService;
        private readonly ILogger<HrApprovalController> _logger;

        public HrApprovalController(timeSheetRecorderContext context, EmailService emailService, ILogger<HrApprovalController> logger)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
        }

        public async Task<IActionResult> Index(int page = 1)
        {
            const int pageSize = 10;

            var query = _context.LeaveRequests
                .Include(lr => lr.LeaveType)
                .Where(lr => lr.Status == LeaveRequestStatus.PendingHR || lr.Status == LeaveRequestStatus.PendingPayrollCapture)
                .OrderBy(lr => lr.CreatedDate);

            var totalRecords = await query.CountAsync();
            var requests = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

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
        public async Task<IActionResult> HrDecision(int id, HrDecisionType decision, string? remarks, string signature)
        {
            var leaveRequest = await _context.LeaveRequests.FirstOrDefaultAsync(lr => lr.Id == id);
            if (leaveRequest == null || leaveRequest.Status != LeaveRequestStatus.PendingHR)
            {
                return NotFound();
            }

            leaveRequest.HrDecision = decision;
            leaveRequest.HrRemarks = remarks;
            leaveRequest.HrSignature = signature;
            leaveRequest.HrDecisionDate = DateTime.Now;
            leaveRequest.HrUserId = User.GetUserId();

            leaveRequest.Status = decision == HrDecisionType.NotApproved
                ? LeaveRequestStatus.RejectedByHR
                : LeaveRequestStatus.PendingPayrollCapture;

            await _context.SaveChangesAsync();
            await NotifyEmployeeAsync(leaveRequest, "HR Manager", $"Decision: {decision}." +
                (string.IsNullOrWhiteSpace(remarks) ? "" : $" Remarks: {remarks}"));

            TempData["SuccessMessage"] = "HR decision recorded.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayrollCapture(int id)
        {
            var leaveRequest = await _context.LeaveRequests.FirstOrDefaultAsync(lr => lr.Id == id);
            if (leaveRequest == null || leaveRequest.Status != LeaveRequestStatus.PendingPayrollCapture)
            {
                return NotFound();
            }

            var capturingUserId = User.GetUserId();
            leaveRequest.CapturedByUserId = capturingUserId;
            leaveRequest.CapturedDate = DateTime.Now;
            leaveRequest.VerifiedByUserId = capturingUserId;
            leaveRequest.VerifiedDate = DateTime.Now;
            leaveRequest.Status = LeaveRequestStatus.Completed;

            // Deduct from the balance only now, at the point leave is actually confirmed - not when
            // first requested, since a request can still be rejected at the Manager or HR stage.
            var year = leaveRequest.StartDate.Year;
            var balance = await _context.LeaveBalances.FirstOrDefaultAsync(b =>
                b.AzureAdUserId == leaveRequest.AzureAdUserId &&
                b.LeaveTypeId == leaveRequest.LeaveTypeId &&
                b.Year == year);

            if (balance == null)
            {
                balance = new LeaveBalance
                {
                    AzureAdUserId = leaveRequest.AzureAdUserId,
                    LeaveTypeId = leaveRequest.LeaveTypeId,
                    Year = year,
                    AccruedDays = 0,
                    UsedDays = 0
                };
                _context.LeaveBalances.Add(balance);
            }

            balance.UsedDays += leaveRequest.TotalDays;

            await _context.SaveChangesAsync();
            await NotifyEmployeeAsync(leaveRequest, "Administration/Payroll",
                $"Your leave has been captured and confirmed. {leaveRequest.TotalDays} day(s) deducted from your balance.");

            TempData["SuccessMessage"] = "Leave captured, balance updated, and request marked complete.";
            return RedirectToAction(nameof(Index));
        }

        private async Task NotifyEmployeeAsync(LeaveRequest leaveRequest, string stageName, string summary)
        {
            var employee = await _context.Users.FirstOrDefaultAsync(u => u.AzureAdUserId == leaveRequest.AzureAdUserId);
            if (employee == null || string.IsNullOrWhiteSpace(employee.Email))
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
    }
}
