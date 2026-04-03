using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using StartRef.Api.Data;

#nullable disable

namespace StartRef.Api.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260403000000_AddCompetitionDateToLookups")]
public class AddCompetitionDateToLookups : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Classes
        migrationBuilder.AddColumn<DateOnly>(
            name: "CompetitionDate",
            table: "Classes",
            type: "date",
            nullable: false,
            defaultValue: new DateOnly(1900, 1, 1));

        migrationBuilder.DropPrimaryKey(
            name: "PK_Classes",
            table: "Classes");

        migrationBuilder.AddPrimaryKey(
            name: "PK_Classes",
            table: "Classes",
            columns: new[] { "CompetitionDate", "Id" });

        // Clubs
        migrationBuilder.AddColumn<DateOnly>(
            name: "CompetitionDate",
            table: "Clubs",
            type: "date",
            nullable: false,
            defaultValue: new DateOnly(1900, 1, 1));

        migrationBuilder.DropPrimaryKey(
            name: "PK_Clubs",
            table: "Clubs");

        migrationBuilder.AddPrimaryKey(
            name: "PK_Clubs",
            table: "Clubs",
            columns: new[] { "CompetitionDate", "Id" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropPrimaryKey(name: "PK_Classes", table: "Classes");
        migrationBuilder.DropColumn(name: "CompetitionDate", table: "Classes");
        migrationBuilder.AddPrimaryKey(name: "PK_Classes", table: "Classes", column: "Id");

        migrationBuilder.DropPrimaryKey(name: "PK_Clubs", table: "Clubs");
        migrationBuilder.DropColumn(name: "CompetitionDate", table: "Clubs");
        migrationBuilder.AddPrimaryKey(name: "PK_Clubs", table: "Clubs", column: "Id");
    }
}
