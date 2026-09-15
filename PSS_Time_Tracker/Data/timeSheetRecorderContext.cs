using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;
using System.Xml.Linq;
using TimeSheetRecorder.Models;
using PSS_Time_Tracker.Models;

namespace PSS_Time_Tracker.Data
{

    public class timeSheetRecorderContext : DbContext
    {



        public DbSet<TimeTrackerModel> TimeTracker { get; set; } 
        public DbSet<UserAccount> Users { get; set; }

        
        public virtual DbSet<SpResult> SpResults { get; set; }

        public DbSet<EmployeeSummaryResult> EmployeeSummaryResults { get; set; }

        public DbSet<TimesheetCountResult> TimesheetCountResults { get; set; }

        public DbSet<LeaveType> LeaveTypes { get; set; }
        public DbSet<LeaveRequest> LeaveRequests { get; set; }
        public DbSet<LeaveBalance> LeaveBalances { get; set; }
        public DbSet<WeeklyTimesheetSignature> WeeklyTimesheetSignatures { get; set; }
        public DbSet<PublicHoliday> PublicHolidays { get; set; }
        public DbSet<GapResolution> GapResolutions { get; set; }
        public DbSet<Notification> Notifications { get; set; }


        public timeSheetRecorderContext(DbContextOptions<timeSheetRecorderContext> options) : base(options)
        {

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<SpResult>().HasNoKey();
            modelBuilder.Entity<EmployeeSummaryResult>().HasNoKey();
            modelBuilder.Entity<TimesheetCountResult>().HasNoKey();

            // One signature row per employee per reported week - GenerateWeeklyReport (employee) and
            // GenerateEmployeeWeeklyReport (manager) both upsert into this row for the same week rather
            // than creating duplicates.
            modelBuilder.Entity<WeeklyTimesheetSignature>()
                .HasIndex(s => new { s.AzureAdUserId, s.WeekStartDate, s.WeekEndDate })
                .IsUnique();

            modelBuilder.Entity<PublicHoliday>()
                .HasIndex(h => h.Date)
                .IsUnique();

            // One resolution per employee per gap date - re-resolving the same date overwrites it
            // (see GapResolution and WeeklyReportGapAnalysisService).
            modelBuilder.Entity<GapResolution>()
                .HasIndex(g => new { g.AzureAdUserId, g.Date })
                .IsUnique();
        }

    }
  
}