using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StartRef.Api.Migrations;

/// <summary>
/// Baseline migration representing the schema as it existed before lookup tables and StartTime.
/// If the database was already created without migrations (e.g. via EnsureCreated or manual DDL),
/// mark this migration as applied without running it:
///   INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
///   VALUES ('20260327000000_InitialCreate', '8.0.0')
/// Then MigrateAsync() will apply only the next migration.
/// </summary>
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "Statuses",
            columns: table => new
            {
                Id = table.Column<int>(nullable: false),
                Name = table.Column<string>(maxLength: 50, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Statuses", x => x.Id);
            });

        migrationBuilder.InsertData("Statuses", ["Id", "Name"], new object[,]
        {
            { 1, "Registered" },
            { 2, "Started" },
            { 3, "DNS" }
        });

        migrationBuilder.CreateTable(
            name: "Competitions",
            columns: table => new
            {
                Date = table.Column<DateOnly>(nullable: false),
                Name = table.Column<string>(nullable: true),
                CreatedAtUtc = table.Column<DateTimeOffset>(nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Competitions", x => x.Date);
            });

        migrationBuilder.CreateTable(
            name: "Runners",
            columns: table => new
            {
                CompetitionDate = table.Column<DateOnly>(nullable: false),
                StartNumber = table.Column<int>(nullable: false),
                SiChipNo = table.Column<string>(nullable: true),
                Name = table.Column<string>(nullable: false, defaultValue: ""),
                Surname = table.Column<string>(nullable: false, defaultValue: ""),
                ClassId = table.Column<int>(nullable: false, defaultValue: 0),
                ClassName = table.Column<string>(nullable: false, defaultValue: ""),
                ClubId = table.Column<int>(nullable: false, defaultValue: 0),
                ClubName = table.Column<string>(nullable: false, defaultValue: ""),
                Country = table.Column<string>(nullable: true),
                StatusId = table.Column<int>(nullable: false, defaultValue: 1),
                StartPlace = table.Column<int>(nullable: false, defaultValue: 0),
                LastModifiedUtc = table.Column<DateTimeOffset>(nullable: false),
                LastModifiedBy = table.Column<string>(nullable: false, defaultValue: "")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Runners", x => new { x.CompetitionDate, x.StartNumber });
                table.ForeignKey("FK_Runners_Competitions", x => x.CompetitionDate,
                    principalTable: "Competitions", principalColumn: "Date");
                table.ForeignKey("FK_Runners_Statuses", x => x.StatusId,
                    principalTable: "Statuses", principalColumn: "Id");
            });

        migrationBuilder.CreateIndex("IX_Runners_CompetitionDate_LastModifiedUtc",
            "Runners", ["CompetitionDate", "LastModifiedUtc"]);

        migrationBuilder.CreateTable(
            name: "ChangeLogEntries",
            columns: table => new
            {
                Id = table.Column<long>(nullable: false).Annotation("SqlServer:Identity", "1, 1"),
                CompetitionDate = table.Column<DateOnly>(nullable: false),
                StartNumber = table.Column<int>(nullable: false),
                FieldName = table.Column<string>(nullable: false, defaultValue: ""),
                OldValue = table.Column<string>(nullable: true),
                NewValue = table.Column<string>(nullable: true),
                ChangedAtUtc = table.Column<DateTimeOffset>(nullable: false),
                ChangedBy = table.Column<string>(nullable: false, defaultValue: "")
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ChangeLogEntries", x => x.Id);
            });

        migrationBuilder.CreateIndex("IX_ChangeLogEntries_CompetitionDate_StartNumber",
            "ChangeLogEntries", ["CompetitionDate", "StartNumber"]);

        migrationBuilder.CreateIndex("IX_ChangeLogEntries_ChangedAtUtc",
            "ChangeLogEntries", "ChangedAtUtc");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("ChangeLogEntries");
        migrationBuilder.DropTable("Runners");
        migrationBuilder.DropTable("Competitions");
        migrationBuilder.DropTable("Statuses");
    }
}
