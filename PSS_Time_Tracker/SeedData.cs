using System.Linq;
using PSS_Time_Tracker.Data;
using PSS_Time_Tracker.Models;
using TimeSheetRecorder.Models;

namespace PSS_Time_Tracker
{
    /// <summary>
    /// Seeds a handful of local test accounts so the app is usable immediately after
    /// `dotnet run`, without Azure AD / Microsoft Graph to supply real employee data.
    /// In the original app this information came from Azure AD; here it's just data.
    /// </summary>
    public static class SeedData
    {
        public static void EnsureSeeded(timeSheetRecorderContext context)
        {
            if (!context.Users.Any())
            {
                context.Users.AddRange(
                    new UserAccount
                    {
                        AzureAdUserId = "u-carol",
                        Email = "carol.manager@local.test",
                        EmployeeName = "Carol",
                        EmployeeSurname = "Manager",
                        JobTitle = "Engineering Manager",
                        SupervisorFullName = ".",
                        SupervisorEmail = "",
                        ApprovalStatus = 1,
                        IsManager = true
                    },
                    new UserAccount
                    {
                        AzureAdUserId = "u-koketso",
                        Email = "koketso.employee@local.test",
                        EmployeeName = "Koketso",
                        EmployeeSurname = "Chabalala",
                        JobTitle = "Software Developer",
                        SupervisorFullName = "Carol Manager",
                        SupervisorEmail = "carol.manager@local.test",
                        ApprovalStatus = 1,
                        IsManager = false
                    },
                    new UserAccount
                    {
                        AzureAdUserId = "u-bob",
                        Email = "bob.employee@local.test",
                        EmployeeName = "Bob",
                        EmployeeSurname = "Dlamini",
                        JobTitle = "Junior Developer",
                        SupervisorFullName = "Carol Manager",
                        SupervisorEmail = "carol.manager@local.test",
                        ApprovalStatus = 1,
                        IsManager = false
                    },
                    new UserAccount
                    {
                        AzureAdUserId = "u-eve",
                        Email = "eve.employee@local.test",
                        EmployeeName = "Eve",
                        EmployeeSurname = "Sithole",
                        JobTitle = "Intern",
                        SupervisorFullName = "Carol Manager",
                        SupervisorEmail = "carol.manager@local.test",
                        ApprovalStatus = 0,
                        IsManager = false
                    }
                );
            }

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

            // Added separately from the block at the top of this method (which only runs once, on a
            // totally empty Users table) so this test account still gets added to an already-seeded
            // database. Matches a real employee in the SharePoint MobileCheckIn list ("Dumisani
            // Nkosi"), so once the SharePoint integration is live his Capture Your Timesheet page
            // pulls real check-in data.
            if (!context.Users.Any(u => u.AzureAdUserId == "u-dumisani"))
            {
                context.Users.Add(new UserAccount
                {
                    AzureAdUserId = "u-dumisani",
                    Email = "dumisani.nkosi@local.test",
                    EmployeeName = "Dumisani",
                    EmployeeSurname = "Nkosi",
                    JobTitle = "Employee",
                    // Placeholder until SyncManagerFromSharePointAsync overwrites this from the real
                    // "Reports to" value the first time he opens Capture Your Timesheet.
                    SupervisorFullName = "Carol Manager",
                    SupervisorEmail = "carol.manager@local.test",
                    ApprovalStatus = 1,
                    IsManager = false
                });
                context.SaveChanges();

                var currentYear = DateTime.Today.Year;
                foreach (var leaveType in context.LeaveTypes.ToList())
                {
                    context.LeaveBalances.Add(new LeaveBalance
                    {
                        AzureAdUserId = "u-dumisani",
                        LeaveTypeId = leaveType.Id,
                        Year = currentYear,
                        AccruedDays = leaveType.DefaultAnnualDays,
                        UsedDays = 0
                    });
                }
                context.SaveChanges();
            }

            // Same pattern as the Dumisani block above - added separately so it lands on an
            // already-seeded database too. A dedicated HR test account, distinct from Carol (the
            // manager) - IsHr is separate from IsManager, so this account can approve/capture leave
            // at the HR/Payroll stage without also seeing the Manager Board or Approve Employees.
            if (!context.Users.Any(u => u.AzureAdUserId == "u-naledi"))
            {
                context.Users.Add(new UserAccount
                {
                    AzureAdUserId = "u-naledi",
                    Email = "naledi.hr@local.test",
                    EmployeeName = "Naledi",
                    EmployeeSurname = "Mokoena",
                    JobTitle = "HR Manager",
                    SupervisorFullName = "Carol Manager",
                    SupervisorEmail = "carol.manager@local.test",
                    ApprovalStatus = 1,
                    IsManager = false,
                    IsHr = true
                });
                context.SaveChanges();

                var currentYear = DateTime.Today.Year;
                foreach (var leaveType in context.LeaveTypes.ToList())
                {
                    context.LeaveBalances.Add(new LeaveBalance
                    {
                        AzureAdUserId = "u-naledi",
                        LeaveTypeId = leaveType.Id,
                        Year = currentYear,
                        AccruedDays = leaveType.DefaultAnnualDays,
                        UsedDays = 0
                    });
                }
                context.SaveChanges();
            }
        }
    }
}
