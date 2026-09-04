using System;
using System.ComponentModel.DataAnnotations;

namespace TimeSheetRecorder.Models.ViewModels
{
    public class TimeTrackerViewModel
    {
     
        public string AzureAdUserId { get; set; } 

        public string EmployeeName { get; set; }
        public string EmployeeSurname { get; set; }
      
        public string JobTitle { get; set; }
   
        public string SupervisorFullName { get; set; }
      

      
        [Required]
        public string TimeSheetMonth { get; set; }
       
        public List<string> MonthOptions { get; set; } = new List<string>();

        [Required]
        public DateTime DateOfEntry { get; set; } = DateTime.Now;

        [Required(ErrorMessage = "Start Time is required.")]
        public DateTime? StartTime { get; set; } 

        [Required(ErrorMessage = "End Time is required.")]
        public DateTime? EndTime { get; set; }

     
        public string DailyTask { get; set; }

 
        public string HostCompanyName { get; set; }

        public List<string> HostCompanyOptions { get; set; } = new List<string>
        {
            "SAPS",
            "SAMSA",
            "SABC",
            "MTN",
            "TigerBrands",
            "PSS"
        };

        
        public List<string> SelectedHostCompanies { get; set; } = new();
        public Dictionary<string, string> DailyTasksPerCompany { get; set; } = new();



      
        public bool IsPublicHoliday { get; set; }

     
        [Display(Name = "Total Hours Worked")]
        public string TotalHrsWorkedDisplay { get; set; }

       
        [Required(ErrorMessage = "Total hours worked is required.")]
        public string TotalHrsWorked { get; set; } 


    }
}
