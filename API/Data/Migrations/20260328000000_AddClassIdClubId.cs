using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StartRef.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddClassIdClubId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ClassId",
                table: "Runners",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "ClubId",
                table: "Runners",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClassId",
                table: "Runners");

            migrationBuilder.DropColumn(
                name: "ClubId",
                table: "Runners");
        }
    }
}
