using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PSS_Time_Tracker.Migrations
{
    /// <inheritdoc />
    public partial class Push1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ApprovalUserCountResults",
                columns: table => new
                {
                    TotalCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "EmployeeSummaryResults",
                columns: table => new
                {
                    AzureAdUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmployeeName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmployeeSurname = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TimesheetCount = table.Column<int>(type: "int", nullable: false),
                    LatestEntry = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "HostCompanies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HostCompanies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SpResults",
                columns: table => new
                {
                    TotalCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "TimesheetCountResults",
                columns: table => new
                {
                    TotalCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                });

            migrationBuilder.CreateTable(
                name: "Users",
                columns: table => new
                {
                    AzureAdUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmployeeName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmployeeSurname = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    JobTitle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ApprovalStatus = table.Column<int>(type: "int", nullable: false),
                    SupervisorFullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SupervisorEmail = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Users", x => x.AzureAdUserId);
                });

            migrationBuilder.CreateTable(
                name: "TimeTracker",
                columns: table => new
                {
                    TimeTrackerId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AzureAdUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmployeeName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EmployeeSurname = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    JobTitle = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SupervisorFullName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    HostCompanyName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TimeSheetMonth = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DateOfEntry = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalHrsWorked = table.Column<double>(type: "float", nullable: false),
                    DailyTask = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsPublicHoliday = table.Column<bool>(type: "bit", nullable: false),
                    UserAccountAzureAdUserId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TimeTracker", x => x.TimeTrackerId);
                    table.ForeignKey(
                        name: "FK_TimeTracker_Users_UserAccountAzureAdUserId",
                        column: x => x.UserAccountAzureAdUserId,
                        principalTable: "Users",
                        principalColumn: "AzureAdUserId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_TimeTracker_UserAccountAzureAdUserId",
                table: "TimeTracker",
                column: "UserAccountAzureAdUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApprovalUserCountResults");

            migrationBuilder.DropTable(
                name: "EmployeeSummaryResults");

            migrationBuilder.DropTable(
                name: "HostCompanies");

            migrationBuilder.DropTable(
                name: "SpResults");

            migrationBuilder.DropTable(
                name: "TimesheetCountResults");

            migrationBuilder.DropTable(
                name: "TimeTracker");

            migrationBuilder.DropTable(
                name: "Users");
        }
    }
}
