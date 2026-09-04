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

        [Required]
        public string HostCompanyName { get; set; }

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

     
        public bool IsPublicHoliday { get; set; }

    }
}
