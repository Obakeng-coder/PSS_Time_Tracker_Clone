using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PSS_Time_Tracker.Migrations
{
    /// <inheritdoc />
    public partial class AddLeaveTypeToGapResolution : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LeaveTypeId",
                table: "GapResolutions",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_GapResolutions_LeaveTypeId",
                table: "GapResolutions",
                column: "LeaveTypeId");

            migrationBuilder.AddForeignKey(
                name: "FK_GapResolutions_LeaveTypes_LeaveTypeId",
                table: "GapResolutions",
                column: "LeaveTypeId",
                principalTable: "LeaveTypes",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GapResolutions_LeaveTypes_LeaveTypeId",
                table: "GapResolutions");

            migrationBuilder.DropIndex(
                name: "IX_GapResolutions_LeaveTypeId",
                table: "GapResolutions");

            migrationBuilder.DropColumn(
                name: "LeaveTypeId",
                table: "GapResolutions");
        }
    }
}
