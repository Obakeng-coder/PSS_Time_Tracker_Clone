using System.ComponentModel.DataAnnotations;

namespace PSS_Time_Tracker.Models
{
    public class HostCompany
    {
        [Key]
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }
    }
}
