using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PSS_Time_Tracker.Data;
using PSS_Time_Tracker.Models;
using System.Linq;
using System.Threading.Tasks;
using PSS_Time_Tracker;
using Hangfire;
using Microsoft.AspNetCore.Authorization;

namespace PSS_Time_Tracker.Controllers
{
 
    public class Admin_Approve_EmployeeController : Controller
    {
        private readonly timeSheetRecorderContext _context;
        private readonly EmailService _emailService;
        private readonly IConfiguration _configuration;
        public Admin_Approve_EmployeeController(timeSheetRecorderContext context, EmailService emailService, IConfiguration configuration)
        {
            _context = context;
            _emailService = emailService;
            _configuration = configuration;
        }



        public async Task<IActionResult> Index(int page = 1, string searchTerm = null)
        {
            int pageSize = 10;
            var searchParam = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm;

           
            var rawManagerName = User.Claims.FirstOrDefault(c => c.Type == "name")?.Value;
            var managerName = rawManagerName?.Split('|')[0].Trim();

         
            var usersNeedingApproval = await _context.Users
                .FromSqlInterpolated($@"
            EXEC GetUsersNeedingApprovalPaged 
                @SearchTerm = {searchParam}, 
                @SupervisorFullName = {managerName},
                @PageNumber = {page}, 
                @PageSize = {pageSize}")
                .AsNoTracking()
                .ToListAsync();

            var totalRecords = _context.ApprovalUserCountResults
                .FromSqlInterpolated($@"
            EXEC GetUsersNeedingApprovalCount 
                @SearchTerm = {searchParam},
                @SupervisorFullName = {managerName}")
                .AsNoTracking()
                .AsEnumerable()
                .Select(r => r.TotalCount)
                .FirstOrDefault();

            var totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.HasPrevious = page > 1;
            ViewBag.HasNext = page < totalPages;
            ViewBag.SearchTerm = searchTerm;

            return View(usersNeedingApproval);
        }


        [HttpPost]
        public async Task<IActionResult> ApproveUser(string azureAdUserId)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.AzureAdUserId == azureAdUserId);

            if (user != null)
            {
                user.ApprovalStatus = 2; 
                _context.Update(user);
                await _context.SaveChangesAsync();

                
                var managerName = User.Identity.Name; 
                var managerEmail = User.Identity.Name; 

                try
                {
                  
                    await _emailService.SendApprovalEmailAsync(
                        user.Email,
                        $"{user.EmployeeName} {user.EmployeeSurname}",
                        managerName,
                        managerEmail);

                    TempData["SuccessMessage"] = $"{user.EmployeeName} {user.EmployeeSurname} has been approved and notified via email.";
                }
                catch (Exception ex)
                {
                   
                    TempData["SuccessMessage"] = $"{user.EmployeeName} {user.EmployeeSurname} has been approved but the email notification failed to send.";
                    TempData["ErrorMessage"] = $"Email sending error: {ex.Message}";
                }
            }
            else
            {
                TempData["ErrorMessage"] = "User not found.";
            }

            return RedirectToAction(nameof(Index));
        }


        [HttpPost]
        public async Task<IActionResult> RejectUser(string azureAdUserId)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.AzureAdUserId == azureAdUserId);

            if (user != null)
            {
                user.ApprovalStatus = 3; 
                _context.Update(user);
                await _context.SaveChangesAsync();



              
                BackgroundJob.Schedule(() => ResetUserStatus(azureAdUserId), TimeSpan.FromHours(24));

              
                var managerName = User.Identity.Name;
                var managerEmail = User.Identity.Name;

                try
                {
                  
                    await _emailService.SendRejectionEmailAsync(
                        user.Email,
                        $"{user.EmployeeName} {user.EmployeeSurname}",
                        managerName,
                        managerEmail);

                    TempData["SuccessMessage"] = $"{user.EmployeeName} {user.EmployeeSurname} has been rejected.";
                }
                catch (Exception ex)
                {
                  
                    TempData["SuccessMessage"] = $"{user.EmployeeName} {user.EmployeeSurname} has been rejected but the email notification failed to send.";
                    TempData["ErrorMessage"] = $"Email sending error: {ex.Message}";
                }
            }
            else
            {
                TempData["ErrorMessage"] = "User not found.";
            }

            return RedirectToAction(nameof(Index));
        }

        [AutomaticRetry(Attempts = 3)]
        public async Task ResetUserStatus(string azureAdUserId)
        {
            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.AzureAdUserId == azureAdUserId);

            if (user != null && user.ApprovalStatus == 3) 
            {
                user.ApprovalStatus = 1; 
                _context.Update(user);
                await _context.SaveChangesAsync();

              
                try
                {
                    await _emailService.SendReapplyNotificationEmailAsync(
                        user.Email,
                        $"{user.EmployeeName} {user.EmployeeSurname}"
                    );
                }
                catch (Exception ex)
                {
                   
                    Console.WriteLine($"Failed to send reapply notification: {ex.Message}");
                }
            }
        }

    }
}