using System.ComponentModel.DataAnnotations;

namespace PSS_Time_Tracker.Models
{
    /// <summary>
    /// Lookup table for the leave categories employees can request (Annual, Sick, Family
    /// Responsibility, Unpaid, Study, Other...). Simple locally-managed data, unlike Work Location
    /// which now comes from SharePoint.
    /// </summary>
    public class LeaveType
    {
        [Key]
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = "";

        /// <summary>Default annual allowance in days, used to seed a new employee's <see cref="LeaveBalance"/>.</summary>
        public int DefaultAnnualDays { get; set; }

        /// <summary>
        /// True for leave types that go through the full Manager -&gt; HR -&gt; Payroll chain.
        /// Kept for future flexibility (e.g. a self-certifying "Study" type); every leave type in the
        /// initial seed data requires approval.
        /// </summary>
        public bool RequiresApproval { get; set; } = true;

        /// <summary>Accent colour for the Team Calendar / UI, e.g. "#c82333".</summary>
        public string ColorHex { get; set; } = "#c82333";
    }
}
