namespace PSS_Time_Tracker.Services
{
    /// <summary>
    /// Everything <see cref="ITimesheetPdfService.GenerateWeeklyReport"/> needs to build the branded
    /// weekly-timesheet PDF, whichever screen is asking for it (an employee generating their own report,
    /// or a manager pulling one for an employee).
    /// </summary>
    public class WeeklyReportPdfRequest
    {
        public string EmployeeName { get; set; } = "";
        public string EmployeeSurname { get; set; } = "";
        public string? JobTitle { get; set; }
        public string? SupervisorFullName { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        /// <summary>One row per weekday in [StartDate, EndDate] - a plain worked day for a simple
        /// employee-generated copy, or the full leave/holiday/gap-resolved breakdown for a manager's
        /// copy (see WeeklyReportGapAnalysisService). Replaces the old raw TimeTrackerModel list so the
        /// PDF can show days that have no underlying TimeTracker row at all (leave, holiday, gap).</summary>
        public List<DayResolution> DayRows { get; set; } = new();

        /// <summary>Typed employee signature, pulled from the persisted <see cref="Models.WeeklyTimesheetSignature"/>
        /// row for this week so it appears whether the employee or the manager is the one generating this PDF.
        /// Blank only if the employee hasn't signed this week yet.</summary>
        public string Signature { get; set; } = "";
        public DateTime? SignatureDate { get; set; }

        /// <summary>Typed supervisor/manager signature, same persisted-row story as <see cref="Signature"/>.
        /// Blank only if the manager hasn't signed this week yet.</summary>
        public string SupervisorSignature { get; set; } = "";
        public DateTime? SupervisorSignatureDate { get; set; }
    }
}
