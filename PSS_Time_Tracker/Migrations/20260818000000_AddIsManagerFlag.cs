using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PSS_Time_Tracker.Migrations
{
    /// <inheritdoc />
    public partial class AddIsManagerFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsManager",
                table: "Users",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsManager",
                table: "Users");
        }
    }
}
