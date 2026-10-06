using TimeSheetRecorder.Models;

namespace PSS_Time_Tracker.Services
{
    public static class TimesheetRules
    {
        // Rule-based anomaly detection (roadmap §7.2): deterministic range checks, not an AI/ML model.
        // Flags an entry for manager review without blocking the save - the employee can still submit,
        // but the flag + reason travel with the row for the Manager Board / timesheet details screens.
        public static (bool Flagged, string? Reason) DetectAnomalies(TimeTrackerModel entry)
        {
            if (entry.IsPublicHoliday)
            {
                return (false, null);
            }

            var reasons = new List<string>();

            if (entry.StartTime.TimeOfDay < TimeSpan.FromHours(5))
            {
                reasons.Add($"Start time ({entry.StartTime:HH:mm}) is before 5:00 AM.");
            }

            if (entry.EndTime.HasValue)
            {
                var shiftLength = entry.EndTime.Value.TimeOfDay - entry.StartTime.TimeOfDay;
                if (shiftLength > TimeSpan.FromHours(12))
                {
                    reasons.Add($"Shift is longer than 12 hours ({FormatHours(shiftLength.TotalHours)}).");
                }
            }
            else if (entry.DateOfEntry.Date < DateTime.Today)
            {
                // Today's entry is simply still in progress; an earlier day with no check-out means the
                // employee never checked out, which a manager needs to know about.
                reasons.Add("No check-out was recorded for this day.");
            }

            return reasons.Count > 0 ? (true, string.Join(" ", reasons)) : (false, null);
        }

        private static string FormatHours(double totalHours)
        {
            int hours = (int)totalHours;
            int minutes = (int)Math.Round((totalHours - hours) * 60);
            return $"{hours}h{minutes:D2}m";
        }
    }
}
