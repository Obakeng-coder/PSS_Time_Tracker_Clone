using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PSS_Time_Tracker.Models
{
    /// <summary>
    /// One row per employee, per leave type, per year. Maps to the "Administration/Payroll: Leave
    /// Available / Leave Granted / Balance" block at the bottom of the paper leave form.
    /// </summary>
    public class LeaveBalance
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string AzureAdUserId { get; set; } = "";

        [Required]
        public int LeaveTypeId { get; set; }

        [ForeignKey(nameof(LeaveTypeId))]
        public LeaveType? LeaveType { get; set; }

        [Required]
        public int Year { get; set; }

        public double AccruedDays { get; set; }
        public double UsedDays { get; set; }

        [NotMapped]
        public double RemainingDays => AccruedDays - UsedDays;
    }
}
