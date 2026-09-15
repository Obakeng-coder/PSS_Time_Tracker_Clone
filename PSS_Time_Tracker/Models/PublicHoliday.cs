using System.ComponentModel.DataAnnotations;

namespace PSS_Time_Tracker.Models
{
    /// <summary>
    /// One row per South African public holiday date. Populated by
    /// <see cref="Services.IPublicHolidayService"/>, which computes the correct set for any given year
    /// (fixed-date holidays + the Easter-derived ones + the Sunday-to-Monday shift required by the
    /// Public Holidays Act) rather than requiring someone to hand-maintain a list every year - this
    /// table is just the cache that computation writes into, and what
    /// WeeklyReportGapAnalysisService reads from when resolving a week's timesheet gaps.
    /// </summary>
    public class PublicHoliday
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        public string Name { get; set; } = "";
    }
}
