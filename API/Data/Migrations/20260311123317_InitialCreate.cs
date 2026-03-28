using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StartRef.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Statuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Statuses", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Competitions",
                columns: table => new
                {
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Competitions", x => x.Date);
                });

            migrationBuilder.CreateTable(
                name: "Runners",
                columns: table => new
                {
                    CompetitionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartNumber = table.Column<int>(type: "int", nullable: false),
                    SiChipNo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Surname = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClassName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ClubName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Country = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StatusId = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    StartPlace = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    LastModifiedUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    LastModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Runners", x => new { x.CompetitionDate, x.StartNumber });
                    table.ForeignKey(
                        name: "FK_Runners_Competitions_CompetitionDate",
                        column: x => x.CompetitionDate,
                        principalTable: "Competitions",
                        principalColumn: "Date",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Runners_Statuses_StatusId",
                        column: x => x.StatusId,
                        principalTable: "Statuses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ChangeLogEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CompetitionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    StartNumber = table.Column<int>(type: "int", nullable: false),
                    FieldName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    OldValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ChangedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ChangedBy = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChangeLogEntries", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "Statuses",
                columns: new[] { "Id", "Name" },
                values: new object[,]
                {
                    { 1, "Registered" },
                    { 2, "Started" },
                    { 3, "DNS" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_Runners_CompetitionDate_LastModifiedUtc",
                table: "Runners",
                columns: new[] { "CompetitionDate", "LastModifiedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Runners_StatusId",
                table: "Runners",
                column: "StatusId");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeLogEntries_ChangedAtUtc",
                table: "ChangeLogEntries",
                column: "ChangedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_ChangeLogEntries_CompetitionDate_StartNumber",
                table: "ChangeLogEntries",
                columns: new[] { "CompetitionDate", "StartNumber" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ChangeLogEntries");
            migrationBuilder.DropTable(name: "Runners");
            migrationBuilder.DropTable(name: "Competitions");
            migrationBuilder.DropTable(name: "Statuses");
        }
    }
}
