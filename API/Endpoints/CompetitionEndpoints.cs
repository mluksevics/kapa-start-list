using Microsoft.EntityFrameworkCore;
using StartRef.Api.Data;
using StartRef.Api.Models;

namespace StartRef.Api.Endpoints;

public static class CompetitionEndpoints
{
    public static void MapCompetitionEndpoints(this WebApplication app)
    {
        app.MapGet("/api/competitions", async (AppDbContext db) =>
        {
            var competitions = await db.Competitions
                .AsNoTracking()
                .OrderByDescending(c => c.Date)
                .Select(c => new CompetitionResponse(c.Date, c.Name, c.CreatedAtUtc))
                .ToListAsync();
            return Results.Ok(competitions);
        });

        // DELETE /api/competitions/{date}/data — clear specific competition date data, keep logs
        app.MapDelete("/api/competitions/{date}/data", async (string date, AppDbContext db) =>
        {
            if (!DateOnly.TryParse(date, out var competitionDate))
                return Results.BadRequest(new { error = "Invalid date format. Use yyyy-MM-dd." });
            return await DeleteCompetitionDataAsync(db, competitionDate);
        });

        // Backward compatibility: DELETE /api/competitions/today/data
        app.MapDelete("/api/competitions/today/data", async (AppDbContext db) =>
        {
            var competitionDate = DateOnly.FromDateTime(DateTime.Today);
            return await DeleteCompetitionDataAsync(db, competitionDate);
        });
    }

    private static async Task<IResult> DeleteCompetitionDataAsync(AppDbContext db, DateOnly competitionDate)
    {
        var deletedRunners = await db.Runners
            .Where(r => r.CompetitionDate == competitionDate)
            .ExecuteDeleteAsync();

        var deletedCompetitions = await db.Competitions
            .Where(c => c.Date == competitionDate)
            .ExecuteDeleteAsync();

        var deletedClasses = await db.Classes.ExecuteDeleteAsync();
        var deletedClubs = await db.Clubs.ExecuteDeleteAsync();

        return Results.Ok(new
        {
            date = competitionDate,
            deletedRunners,
            deletedCompetitions,
            deletedClasses,
            deletedClubs
        });
    }
}
