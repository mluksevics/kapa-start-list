using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StartRef.Api.Data;

#nullable disable

namespace StartRef.Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260331120000_RemoveCountryMoveStartPlaceToClass")]
public class RemoveCountryMoveStartPlaceToClass : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Country",
            table: "Runners");

        migrationBuilder.DropColumn(
            name: "StartPlace",
            table: "Runners");

        migrationBuilder.AddColumn<int>(
            name: "StartPlace",
            table: "Classes",
            type: "int",
            nullable: false,
            defaultValue: 0);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "StartPlace",
            table: "Classes");

        migrationBuilder.AddColumn<string>(
            name: "Country",
            table: "Runners",
            type: "nvarchar(max)",
            nullable: true);

        migrationBuilder.AddColumn<int>(
            name: "StartPlace",
            table: "Runners",
            type: "int",
            nullable: false,
            defaultValue: 0);
    }
}
