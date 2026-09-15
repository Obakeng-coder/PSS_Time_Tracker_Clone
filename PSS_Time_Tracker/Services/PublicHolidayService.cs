using Microsoft.EntityFrameworkCore;
using PSS_Time_Tracker.Data;
using PSS_Time_Tracker.Models;

namespace PSS_Time_Tracker.Services
{
    /// <inheritdoc cref="IPublicHolidayService"/>
    public class PublicHolidayService : IPublicHolidayService
    {
        private readonly timeSheetRecorderContext _context;

        public PublicHolidayService(timeSheetRecorderContext context)
        {
            _context = context;
        }

        public async Task<Dictionary<DateTime, string>> GetHolidaysForYearAsync(int year)
        {
            await EnsureSeededAsync(year);

            var start = new DateTime(year, 1, 1);
            var end = new DateTime(year, 12, 31);
            var rows = await _context.PublicHolidays
                .Where(h => h.Date >= start && h.Date <= end)
                .ToListAsync();

            return rows.ToDictionary(h => h.Date.Date, h => h.Name);
        }

        public async Task<bool> IsPublicHolidayAsync(DateTime date)
        {
            var holidays = await GetHolidaysForYearAsync(date.Year);
            return holidays.ContainsKey(date.Date);
        }

        /// <summary>Idempotent - same pattern as SeedData.cs: inserts whichever of this year's computed
        /// holidays aren't already in the table yet, does nothing if they all already are.</summary>
        private async Task EnsureSeededAsync(int year)
        {
            var start = new DateTime(year, 1, 1);
            var end = new DateTime(year, 12, 31);
            var alreadySeeded = await _context.PublicHolidays.AnyAsync(h => h.Date >= start && h.Date <= end);
            if (alreadySeeded)
            {
                return;
            }

            foreach (var (date, name) in ComputeHolidaysForYear(year))
            {
                _context.PublicHolidays.Add(new PublicHoliday { Date = date, Name = name });
            }

            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// The statutory South African public holidays for a given year: 10 fixed-date holidays plus
        /// Good Friday and Family Day (Easter Monday), both derived from Easter Sunday. Per section 2(1)
        /// of the Public Holidays Act, any holiday that falls on a Sunday is shifted to the following
        /// Monday - unless that Monday is already a holiday for another reason (e.g. Christmas Day
        /// falling on a Sunday means Day of Goodwill, already on the Monday, isn't duplicated).
        /// </summary>
        internal static List<(DateTime Date, string Name)> ComputeHolidaysForYear(int year)
        {
            var easterSunday = ComputeEasterSunday(year);

            var fixedHolidays = new List<(DateTime Date, string Name)>
            {
                (new DateTime(year, 1, 1), "New Year's Day"),
                (new DateTime(year, 3, 21), "Human Rights Day"),
                (easterSunday.AddDays(-2), "Good Friday"),
                (easterSunday.AddDays(1), "Family Day"),
                (new DateTime(year, 4, 27), "Freedom Day"),
                (new DateTime(year, 5, 1), "Workers' Day"),
                (new DateTime(year, 6, 16), "Youth Day"),
                (new DateTime(year, 8, 9), "National Women's Day"),
                (new DateTime(year, 9, 24), "Heritage Day"),
                (new DateTime(year, 12, 16), "Day of Reconciliation"),
                (new DateTime(year, 12, 25), "Christmas Day"),
                (new DateTime(year, 12, 26), "Day of Goodwill"),
            };

            var byDate = new Dictionary<DateTime, string>();
            foreach (var (date, name) in fixedHolidays)
            {
                byDate[date.Date] = name;
            }

            // Sunday -> Monday shift. Collected separately and merged afterwards so a shifted date never
            // overwrites a holiday that's already legitimately on that Monday.
            foreach (var (date, name) in fixedHolidays)
            {
                if (date.DayOfWeek == DayOfWeek.Sunday)
                {
                    var observed = date.AddDays(1);
                    if (!byDate.ContainsKey(observed.Date))
                    {
                        byDate[observed.Date] = $"{name} (Observed)";
                    }
                }
            }

            return byDate.Select(kv => (kv.Key, kv.Value)).OrderBy(h => h.Key).ToList();
        }

        /// <summary>Anonymous Gregorian algorithm (Meeus/Jones/Butcher) for the date of Easter Sunday -
        /// valid for any Gregorian-calendar year.</summary>
        private static DateTime ComputeEasterSunday(int year)
        {
            int a = year % 19;
            int b = year / 100;
            int c = year % 100;
            int d = b / 4;
            int e = b % 4;
            int f = (b + 8) / 25;
            int g = (b - f + 1) / 3;
            int h = (19 * a + b - d - g + 15) % 30;
            int i = c / 4;
            int k = c % 4;
            int l = (32 + 2 * e + 2 * i - h - k) % 7;
            int m = (a + 11 * h + 22 * l) / 451;
            int month = (h + l - 7 * m + 114) / 31;
            int day = ((h + l - 7 * m + 114) % 31) + 1;
            return new DateTime(year, month, day);
        }
    }
}
