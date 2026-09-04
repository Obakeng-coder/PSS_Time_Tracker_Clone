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
                        // Already sitting in "requested admin approval" status so you can see
                        // the Admin > Approve Employees screen populated on first run.
                        ApprovalStatus = 0,
                        IsManager = false
                    }
                );
            }

            if (!context.HostCompanies.Any())
            {
                context.HostCompanies.AddRange(
                    new HostCompany { Name = "PSS" },
                    new HostCompany { Name = "Acme Corp" },
                    new HostCompany { Name = "Globex Ltd" }
                );
            }

            context.SaveChanges();
        }
    }
}
