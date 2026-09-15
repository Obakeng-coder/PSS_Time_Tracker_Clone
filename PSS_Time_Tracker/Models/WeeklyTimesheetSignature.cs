using System.ComponentModel.DataAnnotations;

namespace PSS_Time_Tracker.Models
{
    /// <summary>
    /// One row per employee, per reported week (StartDate/EndDate = the same Monday-Friday range used
    /// to generate a Weekly Time Sheet PDF) - carries the manager's counter-signature for that week, once
    /// one has been captured via ManagerController.GenerateEmployeeWeeklyReport. The employee's own
    /// signature isn't stored here: it's read live from each day's TimeTrackerModel.Signature (captured
    /// at daily submission time in TimeTrackerController.Create), since that's the actual source of
    /// truth. Both GenerateWeeklyReport actions read this row so whichever side regenerates their PDF
    /// after the other has signed still shows both signatures, not just their own.
    /// </summary>
    public class WeeklyTimesheetSignature
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string AzureAdUserId { get; set; } = "";

        [Required]
        public DateTime WeekStartDate { get; set; }

        [Required]
        public DateTime WeekEndDate { get; set; }

        public string? SupervisorSignature { get; set; }
        public DateTime? SupervisorSignatureDate { get; set; }
    }
}
