using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PSS_Time_Tracker.Migrations
{
    /// <inheritdoc />
    public partial class AddHrRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsHr",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsHr",
                table: "Users");
        }
    }
}
