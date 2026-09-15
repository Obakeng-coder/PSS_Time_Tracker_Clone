using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PSS_Time_Tracker.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyTimesheetSignatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WeeklyTimesheetSignatures",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AzureAdUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    WeekStartDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WeekEndDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EmployeeSignature = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EmployeeSignatureDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SupervisorSignature = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SupervisorSignatureDate = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WeeklyTimesheetSignatures", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WeeklyTimesheetSignatures_AzureAdUserId_WeekStartDate_WeekEndDate",
                table: "WeeklyTimesheetSignatures",
                columns: new[] { "AzureAdUserId", "WeekStartDate", "WeekEndDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WeeklyTimesheetSignatures");
        }
    }
}
