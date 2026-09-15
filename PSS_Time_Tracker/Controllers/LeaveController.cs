using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PSS_Time_Tracker.Data;
using PSS_Time_Tracker.Models;
using PSS_Time_Tracker.Services;

namespace PSS_Time_Tracker.Controllers
{
    /// <summary>
    /// Employee-facing side of the Leave Management module: "Request Time Off" and "My Time Off".
    /// Mirrors <see cref="TimeTrackerController"/>'s conventions, but uses classic form POST + redirect
    /// (rather than TimeTrackerController's AJAX/modal flow) to keep this first version simple to verify.
    /// </summary>
    [Authorize]
    public class LeaveController : Controller
    {
        private readonly timeSheetRecorderContext _context;
        private readonly EmailService _emailService;
        private readonly ILogger<LeaveController> _logger;
        private readonly ISharePointLeaveBalanceService _leaveBalanceService;
        private readonly IAzureAdProfileService _azureAdProfileService;
        private readonly INotificationService _notificationService;

        public LeaveController(
            timeSheetRecorderContext context,
            EmailService emailService,
            ILogger<LeaveController> logger,
            ISharePointLeaveBalanceService leaveBalanceService,
            IAzureAdProfileService azureAdProfileService,
            INotificationService notificationService)
        {
            _context = context;
            _emailService = emailService;
            _logger = logger;
            _leaveBalanceService = leaveBalanceService;
            _azureAdProfileService = azureAdProfileService;
            _notificationService = notificationService;
        }

        // Maps a TymSheet LeaveType name to the matching pair of SharePoint LeaveInformation columns.
        // Unpaid/Other have no fixed allowance in SharePoint, so they return null (no cap enforced).
        private static double? RemainingFor(SharePointLeaveBalanceRecord balance, string leaveTypeName) =>
            leaveTypeName.ToLowerInvariant() switch
            {
                "annual" => balance.AnnualRemaining,
                "sick" => balance.SickRemaining,
                "family responsibility" => balance.FamilyResponsibilityRemaining,
                _ => null
            };

        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var userId = User.GetUserId();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.AzureAdUserId == userId);
            var leaveTypes = await _context.LeaveTypes.OrderBy(lt => lt.Name).ToListAsync();

            // Prefers the real Azure AD Department over the local UserAccount value - falls back
            // gracefully (returns null) for the local seeded test accounts, which have no real Azure
            // AD user behind them.
            var azureProfile = user != null ? await _azureAdProfileService.GetProfileAsync(user.Email) : null;

            var model = new LeaveRequestViewModel
            {
                AzureAdUserId = userId,
                EmployeeName = user?.EmployeeName ?? "",
                EmployeeSurname = user?.EmployeeSurname ?? "",
                Department = !string.IsNullOrWhiteSpace(azureProfile?.Department) ? azureProfile!.Department : user?.Department,
                IdNumber = user?.IdNumber,
                EmployeeNumber = user?.EmployeeNumber,
                TelephoneNumber = user?.PhoneNumber,
                LeaveTypeOptions = leaveTypes,
                StartDate = DateTime.Today,
                EndDate = DateTime.Today
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(LeaveRequestViewModel model)
        {
            var userId = User.GetUserId();
            var user = await _context.Users.FirstOrDefaultAsync(u => u.AzureAdUserId == userId);
            var leaveType = await _context.LeaveTypes.FindAsync(model.LeaveTypeId);

            if (leaveType == null)
            {
                ModelState.AddModelError(nameof(model.LeaveTypeId), "Please select a valid leave type.");
            }

            if (leaveType != null && leaveType.Name.Equals("Other", StringComparison.OrdinalIgnoreCase) &&
                string.IsNullOrWhiteSpace(model.OtherLeaveDescription))
            {
                ModelState.AddModelError(nameof(model.OtherLeaveDescription), "Please specify the leave type.");
            }

            if (model.StartDate.HasValue && model.EndDate.HasValue && model.EndDate.Value.Date < model.StartDate.Value.Date)
            {
                ModelState.AddModelError(nameof(model.EndDate), "End Date cannot be before Start Date.");
            }

            double totalDays = 0;
            if (model.StartDate.HasValue && model.EndDate.HasValue && model.EndDate.Value.Date >= model.StartDate.Value.Date)
            {
                // Counts weekdays only, consistent with how the rest of the app treats the work week
                // (TimeTrackerController only ever accepts Monday-Friday entries).
                for (var d = model.StartDate.Value.Date; d <= model.EndDate.Value.Date; d = d.AddDays(1))
                {
                    if (d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday)
                    {
                        totalDays++;
                    }
                }

                if (totalDays == 0)
                {
                    ModelState.AddModelError(nameof(model.EndDate), "The selected range doesn't include any weekdays.");
                }
            }

            // A pending/approved leave request already covering part of this range blocks a new one.
            if (model.StartDate.HasValue && model.EndDate.HasValue)
            {
                var overlapping = await _context.LeaveRequests.AnyAsync(lr =>
                    lr.AzureAdUserId == userId &&
                    lr.Status != LeaveRequestStatus.RejectedByManager &&
                    lr.Status != LeaveRequestStatus.RejectedByHR &&
                    lr.Status != LeaveRequestStatus.Cancelled &&
                    lr.StartDate.Date <= model.EndDate.Value.Date &&
                    lr.EndDate.Date >= model.StartDate.Value.Date);

                if (overlapping)
                {
                    ModelState.AddModelError("", "You already have a leave request covering part of this date range.");
                }

                // A day already logged on a timesheet can't also be taken as leave.
                var hasTimesheetEntry = await _context.TimeTracker.AnyAsync(t =>
                    t.AzureAdUserId == userId &&
                    t.DateOfEntry.Date >= model.StartDate.Value.Date &&
                    t.DateOfEntry.Date <= model.EndDate.Value.Date);

                if (hasTimesheetEntry)
                {
                    ModelState.AddModelError("", "You already have a timesheet entry within this date range. " +
                        "Leave can't be requested for a day already logged as worked.");
                }
            }

            // Balance check against the real SharePoint LeaveInformation numbers - skipped for
            // Unpaid/Other, which have no fixed allowance there.
            double? remainingBalance = null;
            if (leaveType != null && user != null)
            {
                var spBalance = await _leaveBalanceService.GetLeaveBalanceAsync($"{user.EmployeeName} {user.EmployeeSurname}");
                remainingBalance = spBalance != null ? RemainingFor(spBalance, leaveType.Name) : null;

                if (remainingBalance.HasValue && totalDays > remainingBalance.Value)
                {
                    ModelState.AddModelError("", $"This request needs {totalDays} day(s), but only " +
                        $"{remainingBalance.Value} day(s) of {leaveType.Name} leave remain.");
                }
            }

            if (!ModelState.IsValid)
            {
                model.LeaveTypeOptions = await _context.LeaveTypes.OrderBy(lt => lt.Name).ToListAsync();
                model.RemainingBalance = remainingBalance;
                model.EmployeeName = user?.EmployeeName ?? "";
                model.EmployeeSurname = user?.EmployeeSurname ?? "";
                return View(model);
            }

            var leaveRequest = new LeaveRequest
            {
                AzureAdUserId = userId,
                LeaveTypeId = model.LeaveTypeId,
                OtherLeaveDescription = model.OtherLeaveDescription,
                StartDate = model.StartDate!.Value,
                EndDate = model.EndDate!.Value,
                TotalDays = totalDays,
                AddressDuringLeave = model.AddressDuringLeave,
                TelephoneNumber = model.TelephoneNumber,
                Reason = model.Reason,
                EmployeeSignature = model.EmployeeSignature,
                EmployeeSignatureDate = DateTime.Now,
                Status = LeaveRequestStatus.PendingManager
            };

            _context.LeaveRequests.Add(leaveRequest);
            await _context.SaveChangesAsync();

            if (user != null && !string.IsNullOrWhiteSpace(user.SupervisorEmail))
            {
                // In-app "bell" notification - independent of the email below (see
                // INotificationService), so a manager still gets notified even when SMTP isn't
                // configured/fails, same as it should the other way around.
                var manager = await _context.Users.FirstOrDefaultAsync(u =>
                    u.Email != null && u.Email.ToLower() == user.SupervisorEmail.ToLower());
                if (manager != null)
                {
                    await _notificationService.CreateAsync(
                        manager.AzureAdUserId, NotificationType.LeaveRequestSubmitted,
                        $"{user.EmployeeName} {user.EmployeeSurname} requested {leaveType!.Name} leave",
                        $"{leaveRequest.StartDate:yyyy-MM-dd} to {leaveRequest.EndDate:yyyy-MM-dd}. Review it on Approve Time Off.",
                        "/LeaveApproval/Index");
                }

                try
                {
                    await _emailService.SendLeaveRequestToManagerAsync(
                        user.SupervisorEmail,
                        user.SupervisorFullName ?? "Manager",
                        $"{user.EmployeeName} {user.EmployeeSurname}",
                        leaveType!.Name,
                        leaveRequest.StartDate,
                        leaveRequest.EndDate);

                    TempData["SuccessMessage"] = "Your leave request has been submitted and your manager notified.";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to send leave request notification email");
                    TempData["SuccessMessage"] = "Your leave request has been submitted, but the email " +
                        "notification to your manager failed to send.";
                }
            }
            else
            {
                TempData["SuccessMessage"] = "Your leave request has been submitted.";
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Index(int page = 1)
        {
            var userId = User.GetUserId();
            const int pageSize = 10;

            var query = _context.LeaveRequests
                .Include(lr => lr.LeaveType)
                .Where(lr => lr.AzureAdUserId == userId)
                .OrderByDescending(lr => lr.CreatedDate);

            var totalRecords = await query.CountAsync();
            var requests = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            var user = await _context.Users.FirstOrDefaultAsync(u => u.AzureAdUserId == userId);
            var currentYear = DateTime.Today.Year;

            // Built from live SharePoint LeaveInformation data rather than a locally-tracked table -
            // real allowances/usage, matched by name. Falls back to an empty list if the integration
            // isn't configured yet or this employee has no matching row there.
            var balances = new List<LeaveBalance>();
            if (user != null)
            {
                var spBalance = await _leaveBalanceService.GetLeaveBalanceAsync($"{user.EmployeeName} {user.EmployeeSurname}");
                if (spBalance != null)
                {
                    var leaveTypes = await _context.LeaveTypes.ToListAsync();
                    foreach (var leaveType in leaveTypes)
                    {
                        var remaining = RemainingFor(spBalance, leaveType.Name);
                        if (remaining == null)
                        {
                            continue; // No SharePoint column maps to this type (e.g. Unpaid/Other) - nothing to show.
                        }

                        var (accrued, used) = leaveType.Name.ToLowerInvariant() switch
                        {
                            "annual" => (spBalance.AnnualLeave, spBalance.AnnualLeavesUsed),
                            "sick" => (spBalance.SickLeave, spBalance.SicksLeavesUsed),
                            "family responsibility" => (spBalance.FamilyResponsibilityLeave, spBalance.FamilyResponsibilityLeaveUsed),
                            _ => (0.0, 0.0)
                        };

                        balances.Add(new LeaveBalance
                        {
                            AzureAdUserId = userId!,
                            LeaveTypeId = leaveType.Id,
                            LeaveType = leaveType,
                            Year = currentYear,
                            AccruedDays = accrued,
                            UsedDays = used
                        });
                    }

                    // Unpaid has no fixed allowance in SharePoint (RemainingFor returns null for it,
                    // deliberately, so it's never capped on the request-validation side) - shown here
                    // anyway as a used-days-only card, per the full set of columns that should appear
                    // under Leave Balance.
                    var unpaidType = leaveTypes.FirstOrDefault(lt => lt.Name.Equals("Unpaid", StringComparison.OrdinalIgnoreCase));
                    if (unpaidType != null)
                    {
                        balances.Add(new LeaveBalance
                        {
                            AzureAdUserId = userId!,
                            LeaveTypeId = unpaidType.Id,
                            LeaveType = unpaidType,
                            Year = currentYear,
                            AccruedDays = 0, // No cap - the view shows "Unlimited" rather than "0 days left".
                            UsedDays = spBalance.UnpaidLeaves
                        });
                    }
                }
                ViewBag.MaternityLeave = spBalance?.MaternityLeave;
                ViewBag.PaternityLeave = spBalance?.PaternityLeave;
            }

            ViewBag.Balances = balances;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);
            ViewBag.HasPrevious = page > 1;
            ViewBag.HasNext = page < ViewBag.TotalPages;

            return View(requests);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var userId = User.GetUserId();
            var leaveRequest = await _context.LeaveRequests
                .FirstOrDefaultAsync(lr => lr.Id == id && lr.AzureAdUserId == userId);

            if (leaveRequest == null)
            {
                return NotFound();
            }

            // Only a request that hasn't finished the approval chain (and isn't already terminal) can
            // still be withdrawn - a Completed request has already deducted the balance.
            if (leaveRequest.Status is LeaveRequestStatus.PendingManager or LeaveRequestStatus.PendingHR
                or LeaveRequestStatus.PendingPayrollCapture)
            {
                leaveRequest.Status = LeaveRequestStatus.Cancelled;
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = "Your leave request has been withdrawn.";
            }
            else
            {
                TempData["ErrorMessage"] = "This request can no longer be withdrawn.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
