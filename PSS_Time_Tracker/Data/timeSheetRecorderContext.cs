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

        public DbSet<ApprovalUserCountResult> ApprovalUserCountResults { get; set; }
        
        public DbSet<HostCompany> HostCompanies { get; set; }


        public timeSheetRecorderContext(DbContextOptions<timeSheetRecorderContext> options) : base(options)
        {

        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<SpResult>().HasNoKey();
            modelBuilder.Entity<EmployeeSummaryResult>().HasNoKey();
            modelBuilder.Entity<TimesheetCountResult>().HasNoKey();
            modelBuilder.Entity<ApprovalUserCountResult>().HasNoKey();
        }

    }
  
}