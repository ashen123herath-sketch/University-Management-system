using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace UniManage.Migrations
{
    /// <inheritdoc />
    public partial class AddAssignmentFilePath : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FilePath",
                table: "Assignments",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FileType",
                table: "Assignments",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FilePath",
                table: "Assignments");

            migrationBuilder.DropColumn(
                name: "FileType",
                table: "Assignments");
        }
    }
}
