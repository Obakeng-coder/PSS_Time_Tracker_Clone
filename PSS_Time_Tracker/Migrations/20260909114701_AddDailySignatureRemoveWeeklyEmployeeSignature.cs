using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PSS_Time_Tracker.Migrations
{
    /// <inheritdoc />
    public partial class AddDailySignatureRemoveWeeklyEmployeeSignature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "EmployeeSignature",
                table: "WeeklyTimesheetSignatures");

            migrationBuilder.DropColumn(
                name: "EmployeeSignatureDate",
                table: "WeeklyTimesheetSignatures");

            migrationBuilder.AddColumn<string>(
                name: "Signature",
                table: "TimeTracker",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Signature",
                table: "TimeTracker");

            migrationBuilder.AddColumn<string>(
                name: "EmployeeSignature",
                table: "WeeklyTimesheetSignatures",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmployeeSignatureDate",
                table: "WeeklyTimesheetSignatures",
                type: "datetime2",
                nullable: true);
        }
    }
}
