using System.Linq;
using PSS_Time_Tracker.Data;
using PSS_Time_Tracker.Models;
using TimeSheetRecorder.Models;

namespace PSS_Time_Tracker
{
    /// <summary>
    /// Seeds reference data (leave types) that has to exist regardless of who's actually signed
    /// in. Used to also seed a handful of fake @local.test employees for testing without Azure AD -
    /// removed now that real sign-in (Program.cs's Microsoft.Identity.Web wiring) is live: every
    /// employee's Users row is real profile data provisioned by HR/IT ahead of their first sign-in,
    /// not something this app invents on startup. See AccountController for how sign-in refuses
    /// anyone Azure AD authenticates who isn't already such a row.
    /// </summary>
    public static class SeedData
    {
        public static void EnsureSeeded(timeSheetRecorderContext context)
        {
            if (!context.LeaveTypes.Any())
            {
                context.LeaveTypes.AddRange(
                    new LeaveType { Name = "Annual", DefaultAnnualDays = 15, ColorHex = "#c82333" },
                    new LeaveType { Name = "Sick", DefaultAnnualDays = 10, ColorHex = "#1f6feb" },
                    new LeaveType { Name = "Family Responsibility", DefaultAnnualDays = 3, ColorHex = "#8957e5" },
                    new LeaveType { Name = "Unpaid", DefaultAnnualDays = 0, ColorHex = "#6e7781" },
                    // Matches the paper form's "Other Leave (Please Specify)" checkbox.
                    new LeaveType { Name = "Other", DefaultAnnualDays = 0, ColorHex = "#bf8700" }
                );
            }

            context.SaveChanges();

            // Seed this year's leave balance for every existing employee, for every leave type, using
            // that type's DefaultAnnualDays - only runs once (guarded above by LeaveTypes.Any()), so
            // this executes on the same first run as the rest of the seed data.
            if (!context.LeaveBalances.Any())
            {
                var currentYear = DateTime.Today.Year;
                var leaveTypeIds = context.LeaveTypes.Select(lt => new { lt.Id, lt.DefaultAnnualDays }).ToList();

                foreach (var user in context.Users)
                {
                    foreach (var leaveType in leaveTypeIds)
                    {
                        context.LeaveBalances.Add(new LeaveBalance
                        {
                            AzureAdUserId = user.AzureAdUserId,
                            LeaveTypeId = leaveType.Id,
                            Year = currentYear,
                            AccruedDays = leaveType.DefaultAnnualDays,
                            UsedDays = 0
                        });
                    }
                }

                context.SaveChanges();
            }
        }
    }
}
