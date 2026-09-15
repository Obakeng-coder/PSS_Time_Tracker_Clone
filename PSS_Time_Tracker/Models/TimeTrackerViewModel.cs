using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using PSS_Time_Tracker.Models;

namespace TimeSheetRecorder.Models.ViewModels
{
    /// <summary>
    /// View model for Capture Your Timesheet. As of the SharePoint integration, Employee Name and
    /// Start/End Time are pulled live from the SharePoint MobileCheckIn list and shown read-only - the
    /// employee only edits the Date and Work Location. See docs/SharePointIntegration.md.
    /// </summary>
    public class TimeTrackerViewModel
    {
        public string AzureAdUserId { get; set; }

        // Read-only display fields - pulled from UserAccount / SharePoint, not posted back as editable.
        public string EmployeeName { get; set; }
        public string EmployeeSurname { get; set; }
        public string JobTitle { get; set; }
        public string SupervisorFullName { get; set; }

        [Required]
        public string TimeSheetMonth { get; set; }

        [Required]
        public DateTime DateOfEntry { get; set; } = DateTime.Now;

        /// <summary>Read-only - pulled from SharePoint for DateOfEntry. Null if no check-in was found yet.</summary>
        public DateTime? StartTime { get; set; }

        /// <summary>Read-only - pulled from SharePoint for DateOfEntry. Null if no check-out was found yet.</summary>
        public DateTime? EndTime { get; set; }

        public string DailyTask { get; set; }

        /// <summary>Typed signature confirming this specific day's entry - required on every submission
        /// (enforced in TimeTrackerController.Create, same pattern as DailyTask) so the week's
        /// weekly-report PDF always has a real Employee Signature to pull from.</summary>
        public string Signature { get; set; } = "";

        /// <summary>Read-only - pulled from SharePoint for DateOfEntry, never editable/selectable by the employee.</summary>
        [Required(ErrorMessage = "No Work Location was found on your SharePoint check-in record for this date.")]
        public string WorkLocation { get; set; }

        public bool IsPublicHoliday { get; set; }

        /// <summary>Drives the "Day Type" dropdown that replaced the old plain "Is Today a Public
        /// Holiday?" checkbox: "Regular" (default, a normal worked day), "PublicHoliday", or a
        /// LeaveTypeId (as a string) - flagging this day as leave straight from Capture Your Timesheet
        /// instead of filing a formal Request Time Off application. See TimeTrackerController.Create.</summary>
        public string DayType { get; set; } = "Regular";

        /// <summary>Populates the leave-type half of the Day Type dropdown - not posted back, just
        /// re-rendered from LeaveTypes on a validation-failure redisplay.</summary>
        public List<LeaveType> LeaveTypeOptions { get; set; } = new();

        /// <summary>Optional free-text note when flagging a day as leave via the dropdown - carried
        /// through to the manager's Timesheet Gaps panel same as any other self-service proposal.</summary>
        public string? LeaveReason { get; set; }

        /// <summary>True once a SharePoint check-in record was actually found for DateOfEntry.</summary>
        public bool HasSharePointCheckIn { get; set; }
    }
}
