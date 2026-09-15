namespace PSS_Time_Tracker.Services
{
    /// <summary>
    /// One employee's row from the SharePoint "LeaveInformation" list - the real leave allowances and
    /// used-so-far counts, replacing the invented defaults TymSheet's own SeedData used before this.
    /// </summary>
    public class SharePointLeaveBalanceRecord
    {
        /// <summary>From "displayName" - stored as "Name | Company"; only the name portion is kept.</summary>
        public string? DisplayName { get; set; }

        public double AnnualLeave { get; set; }
        public double AnnualLeavesUsed { get; set; }
        public double SickLeave { get; set; }
        public double SicksLeavesUsed { get; set; }
        public double FamilyResponsibilityLeave { get; set; }
        public double FamilyResponsibilityLeaveUsed { get; set; }
        public double UnpaidLeaves { get; set; }

        /// <summary>
        /// Free text (e.g. "10 days", "4 Months") - units aren't consistent across rows, so this is
        /// shown as-is rather than turned into a day count for balance arithmetic.
        /// </summary>
        public string? MaternityLeave { get; set; }
        public string? PaternityLeave { get; set; }

        public double AnnualRemaining => AnnualLeave - AnnualLeavesUsed;
        public double SickRemaining => SickLeave - SicksLeavesUsed;
        public double FamilyResponsibilityRemaining => FamilyResponsibilityLeave - FamilyResponsibilityLeaveUsed;
    }

    /// <summary>
    /// Reads real leave allowances/usage from the SharePoint "LeaveInformation" list via Microsoft
    /// Graph, matched by employee full name against that list's "displayName" column (which stores
    /// "Name | Company", e.g. "Malk Mokgoshi | Providence Soft ZA").
    /// </summary>
    public interface ISharePointLeaveBalanceService
    {
        Task<SharePointLeaveBalanceRecord?> GetLeaveBalanceAsync(string employeeFullName);
    }
}
