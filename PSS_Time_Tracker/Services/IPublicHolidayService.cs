namespace PSS_Time_Tracker.Services
{
    /// <summary>
    /// South African public holidays (Public Holidays Act 36 of 1994) - the fixed-date ones, the two
    /// Easter-derived ones (Good Friday, Family Day), and the "falls on a Sunday -> the following
    /// Monday is also a public holiday" rule. Computed algorithmically for any year (see
    /// PublicHolidayService.ComputeHolidaysForYear) rather than hand-maintained, then cached into the
    /// PublicHolidays table so WeeklyReportGapAnalysisService can query it like any other table.
    /// </summary>
    public interface IPublicHolidayService
    {
        /// <summary>Computes (if not already cached) and returns every public holiday date for
        /// <paramref name="year"/>, keyed by date, for fast lookup.</summary>
        Task<Dictionary<DateTime, string>> GetHolidaysForYearAsync(int year);

        /// <summary>True if <paramref name="date"/> (any year) is a South African public holiday.</summary>
        Task<bool> IsPublicHolidayAsync(DateTime date);
    }
}
