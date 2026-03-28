using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using StartRef.Api.Data;

#nullable disable

namespace StartRef.Api.Migrations;

/// <summary>
/// Introduces Class/Club lookup tables, migrates inline name columns to those tables,
/// drops ClassName/ClubName from Runners, and adds StartTime (time NULL) to Runners.
/// </summary>
[DbContext(typeof(AppDbContext))]
[Migration("20260328000001_LookupTablesAndStartTime")]
public partial class LookupTablesAndStartTime : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. Create lookup tables
        migrationBuilder.CreateTable(
            name: "Classes",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false),
                Name = table.Column<string>(maxLength: 200, nullable: false, defaultValue: "")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Classes", x => x.Id);
            });

        migrationBuilder.CreateTable(
            name: "Clubs",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false),
                Name = table.Column<string>(maxLength: 200, nullable: false, defaultValue: "")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Clubs", x => x.Id);
            });

        // 2. Migrate existing name data into lookup tables
        migrationBuilder.Sql(@"
            INSERT INTO Classes (Id, Name)
            SELECT DISTINCT ClassId, ClassName
            FROM Runners
            WHERE ClassId > 0 AND ClassName IS NOT NULL AND ClassName <> ''
        ");

        migrationBuilder.Sql(@"
            INSERT INTO Clubs (Id, Name)
            SELECT DISTINCT ClubId, ClubName
            FROM Runners
            WHERE ClubId > 0 AND ClubName IS NOT NULL AND ClubName <> ''
        ");

        // 3. Drop inline name columns from Runners
        migrationBuilder.DropColumn(name: "ClassName", table: "Runners");
        migrationBuilder.DropColumn(name: "ClubName", table: "Runners");

        // 4. Add StartTime column
        migrationBuilder.AddColumn<TimeOnly>(
            name: "StartTime",
            table: "Runners",
            type: "time",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "StartTime", table: "Runners");

        migrationBuilder.AddColumn<string>(
            name: "ClassName",
            table: "Runners",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "ClubName",
            table: "Runners",
            nullable: false,
            defaultValue: "");

        // Restore names from lookup tables
        migrationBuilder.Sql(@"
            UPDATE r SET r.ClassName = c.Name
            FROM Runners r JOIN Classes c ON r.ClassId = c.Id
        ");
        migrationBuilder.Sql(@"
            UPDATE r SET r.ClubName = cl.Name
            FROM Runners r JOIN Clubs cl ON r.ClubId = cl.Id
        ");

        migrationBuilder.DropTable(name: "Classes");
        migrationBuilder.DropTable(name: "Clubs");
    }
}
