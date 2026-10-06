namespace PSS_Time_Tracker.Services
{
    /// <summary>
    /// Realistic stand-in for <see cref="SharePointLeaveBalanceService"/> - see
    /// MockSharePointCheckInService for why this exists and the "(Mock)" labelling convention.
    /// </summary>
    public class MockSharePointLeaveBalanceService : ISharePointLeaveBalanceService
    {
        public Task<SharePointLeaveBalanceRecord?> GetLeaveBalanceAsync(string employeeEmail)
        {
            var seed = Math.Abs(employeeEmail.GetHashCode());

            return Task.FromResult<SharePointLeaveBalanceRecord?>(new SharePointLeaveBalanceRecord
            {
                DisplayName = $"(Mock) {employeeEmail}",
                AnnualLeave = 15,
                AnnualLeavesUsed = seed % 10,
                SickLeave = 30,
                SicksLeavesUsed = seed % 5,
                FamilyResponsibilityLeave = 3,
                FamilyResponsibilityLeaveUsed = seed % 4,
                UnpaidLeaves = 0,
                MaternityLeave = "(Mock) 10 days",
                PaternityLeave = null
            });
        }
    }
}
