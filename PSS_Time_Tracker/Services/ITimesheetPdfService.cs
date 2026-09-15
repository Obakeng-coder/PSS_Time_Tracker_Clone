namespace PSS_Time_Tracker.Services
{
    /// <summary>
    /// Builds the branded weekly-timesheet PDF. Extracted so
    /// <see cref="PSS_Time_Tracker.Controllers.TimeTrackerController.GenerateWeeklyReport"/> and
    /// <see cref="PSS_Time_Tracker.Controllers.ManagerController.GenerateEmployeeWeeklyReport"/> share one
    /// implementation instead of two near-identical copies.
    /// </summary>
    public interface ITimesheetPdfService
    {
        /// <returns>The rendered PDF as a byte array, ready to return via <c>File(bytes, "application/pdf", fileName)</c>.</returns>
        byte[] GenerateWeeklyReport(WeeklyReportPdfRequest request);
    }
}
