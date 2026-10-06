using System;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using PSS_Time_Tracker.Data;

#nullable disable

namespace PSS_Time_Tracker.Migrations
{
    /// <summary>
    /// A timesheet entry can now be saved with a check-in only: EndTime (and the hours worked) stay
    /// empty until the check-out is picked up from SharePoint later.
    /// </summary>
    [DbContext(typeof(timeSheetRecorderContext))]
    [Migration("20261006090000_AllowPendingCheckOut")]
    public partial class AllowPendingCheckOut : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<DateTime>(
                name: "EndTime",
                table: "TimeTracker",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE TimeTracker SET EndTime = StartTime WHERE EndTime IS NULL");

            migrationBuilder.AlterColumn<DateTime>(
                name: "EndTime",
                table: "TimeTracker",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified),
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);
        }
    }
}
