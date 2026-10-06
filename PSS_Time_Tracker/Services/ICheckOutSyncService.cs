namespace PSS_Time_Tracker.Services
{
    /// <summary>
    /// Fills in the check-out time and total hours on timesheet entries that were saved with only a
    /// check-in. Runs on a schedule for everyone, and on demand for one employee right before their
    /// timesheet is shown or exported.
    /// </summary>
    public interface ICheckOutSyncService
    {
        /// <returns>How many entries were completed.</returns>
        Task<int> SyncUserAsync(string azureAdUserId);

        /// <returns>How many entries were completed.</returns>
        Task<int> SyncAllPendingAsync();

        /// <summary>
        /// Tries to complete this employee's pending entries, then returns the worked days in the range
        /// that still have no check-out - a weekly PDF must not be exported while any remain, since every
        /// exported timesheet has to carry check-in, check-out and total hours.
        /// </summary>
        Task<List<DateTime>> GetMissingCheckOutDatesAsync(string azureAdUserId, DateTime startDate, DateTime endDate);
    }
}
