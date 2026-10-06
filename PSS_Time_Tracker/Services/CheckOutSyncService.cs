using Microsoft.EntityFrameworkCore;
using PSS_Time_Tracker.Data;

namespace PSS_Time_Tracker.Services
{
    /// <inheritdoc cref="ICheckOutSyncService"/>
    public class CheckOutSyncService : ICheckOutSyncService
    {
        // Older than this and a missing check-out is treated as never coming - it stays flagged for the
        // manager instead of being re-queried against SharePoint forever.
        private const int LookbackDays = 14;

        private readonly timeSheetRecorderContext _context;
        private readonly ISharePointCheckInService _checkInService;
        private readonly ILogger<CheckOutSyncService> _logger;

        public CheckOutSyncService(
            timeSheetRecorderContext context, ISharePointCheckInService checkInService, ILogger<CheckOutSyncService> logger)
        {
            _context = context;
            _checkInService = checkInService;
            _logger = logger;
        }

        public Task<int> SyncUserAsync(string azureAdUserId) => SyncAsync(azureAdUserId);

        public Task<int> SyncAllPendingAsync() => SyncAsync(null);

        public async Task<List<DateTime>> GetMissingCheckOutDatesAsync(string azureAdUserId, DateTime startDate, DateTime endDate)
        {
            await SyncAsync(azureAdUserId);

            return await _context.TimeTracker
                .Where(t => t.AzureAdUserId == azureAdUserId && t.EndTime == null && !t.IsPublicHoliday &&
                            t.DateOfEntry >= startDate && t.DateOfEntry <= endDate)
                .OrderBy(t => t.DateOfEntry)
                .Select(t => t.DateOfEntry.Date)
                .ToListAsync();
        }

        private async Task<int> SyncAsync(string? azureAdUserId)
        {
            var since = DateTime.Today.AddDays(-LookbackDays);

            var pending = await _context.TimeTracker
                .Where(t => t.EndTime == null && !t.IsPublicHoliday && t.DateOfEntry >= since &&
                            (azureAdUserId == null || t.AzureAdUserId == azureAdUserId))
                .ToListAsync();

            var completed = 0;
            foreach (var entry in pending)
            {
                try
                {
                    var checkIn = await _checkInService.GetCheckInForDateAsync(
                        $"{entry.EmployeeName} {entry.EmployeeSurname}", entry.DateOfEntry);

                    // Real-world guard: a check-out at or before the check-in is bad data, not a finished
                    // day - leave the entry pending rather than recording zero/negative hours.
                    if (checkIn?.CheckOutTime is not { } checkOut || checkOut <= entry.StartTime)
                    {
                        continue;
                    }

                    entry.EndTime = checkOut;
                    entry.TotalHrsWorked = Math.Round((checkOut - entry.StartTime).TotalHours, 2);

                    var (flagged, reason) = TimesheetRules.DetectAnomalies(entry);
                    entry.IsFlaggedForReview = flagged;
                    entry.FlagReason = reason;
                    completed++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Couldn't look up the check-out for {Name} on {Date}", entry.EmployeeName, entry.DateOfEntry);
                }
            }

            if (completed > 0)
            {
                await _context.SaveChangesAsync();
            }

            return completed;
        }
    }
}
