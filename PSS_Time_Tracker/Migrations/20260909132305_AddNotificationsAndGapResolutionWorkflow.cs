using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PSS_Time_Tracker.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationsAndGapResolutionWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "ResolvedDate",
                table: "GapResolutions",
                newName: "RequestedDate");

            migrationBuilder.RenameColumn(
                name: "ResolvedByUserId",
                table: "GapResolutions",
                newName: "RequestedByUserId");

            migrationBuilder.AddColumn<string>(
                name: "ConfirmedByUserId",
                table: "GapResolutions",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConfirmedDate",
                table: "GapResolutions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "GapResolutions",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RecipientUserId = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Url = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropColumn(
                name: "ConfirmedByUserId",
                table: "GapResolutions");

            migrationBuilder.DropColumn(
                name: "ConfirmedDate",
                table: "GapResolutions");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "GapResolutions");

            migrationBuilder.RenameColumn(
                name: "RequestedDate",
                table: "GapResolutions",
                newName: "ResolvedDate");

            migrationBuilder.RenameColumn(
                name: "RequestedByUserId",
                table: "GapResolutions",
                newName: "ResolvedByUserId");
        }
    }
}
