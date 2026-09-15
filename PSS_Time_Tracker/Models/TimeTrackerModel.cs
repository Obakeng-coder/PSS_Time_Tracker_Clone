using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;


namespace TimeSheetRecorder.Models
{
   
    public class TimeTrackerModel
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int TimeTrackerId { get; set; }

  
        [Required]
        public string AzureAdUserId { get; set; }

        
        public string EmployeeName { get; set; }
        public string EmployeeSurname { get; set; }
        public string JobTitle { get; set; }
        public string SupervisorFullName { get; set; }

        // Replaces HostCompanyName - pulled from the "Work Location" column of the SharePoint
        // MobileCheckIn list (see Services/SharePointCheckInService.cs) rather than a locally-managed
        // dropdown.
        [Required]
        public string WorkLocation { get; set; }

        [Required]
        public string TimeSheetMonth { get; set; }

        [Required]
        public DateTime DateOfEntry { get; set; } = DateTime.Now;

        [Required]
        public DateTime StartTime { get; set; }

        [Required]
        public DateTime EndTime { get; set; }

       public double TotalHrsWorked { get; set; } 


        public string DailyTask { get; set; }

        // Typed employee signature captured on this specific daily entry at submission time. Once every
        // entry in a Mon-Fri week carries one, it's what TimeTrackerController.GenerateWeeklyReport and
        // ManagerController.GenerateEmployeeWeeklyReport use as the Employee Signature on the exported
        // weekly PDF - see WeeklyReportPdfRequest.Signature.
        [Required]
        public string Signature { get; set; } = "";

        public bool IsPublicHoliday { get; set; }

        // Rule-based anomaly flagging (see TimeTrackerController.Create) - plain range checks on
        // Start/End time, not an AI/ML model. Surfaced to managers as a review flag rather than
        // blocking the entry outright.
        public bool IsFlaggedForReview { get; set; }
        public string? FlagReason { get; set; }

    }
}
