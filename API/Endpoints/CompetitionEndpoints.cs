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

        // DELETE /api/competitions/today/data — clear today's competition data, keep logs
        app.MapDelete("/api/competitions/today/data", async (AppDbContext db) =>
        {
            var today = DateOnly.FromDateTime(DateTime.Today);

            var classIdsToDelete = await db.Runners
                .AsNoTracking()
                .Where(r => r.CompetitionDate == today && r.ClassId > 0)
                .Select(r => r.ClassId)
                .Distinct()
                .ToListAsync();

            var clubIdsToDelete = await db.Runners
                .AsNoTracking()
                .Where(r => r.CompetitionDate == today && r.ClubId > 0)
                .Select(r => r.ClubId)
                .Distinct()
                .ToListAsync();

            await using var tx = await db.Database.BeginTransactionAsync();

            var deletedRunners = await db.Runners
                .Where(r => r.CompetitionDate == today)
                .ExecuteDeleteAsync();

            var deletedCompetitions = await db.Competitions
                .Where(c => c.Date == today)
                .ExecuteDeleteAsync();

            var deletedClasses = 0;
            if (classIdsToDelete.Count > 0)
            {
                deletedClasses = await db.Classes
                    .Where(c => classIdsToDelete.Contains(c.Id) && !db.Runners.Any(r => r.ClassId == c.Id))
                    .ExecuteDeleteAsync();
            }

            var deletedClubs = 0;
            if (clubIdsToDelete.Count > 0)
            {
                deletedClubs = await db.Clubs
                    .Where(c => clubIdsToDelete.Contains(c.Id) && !db.Runners.Any(r => r.ClubId == c.Id))
                    .ExecuteDeleteAsync();
            }

            await tx.CommitAsync();

            return Results.Ok(new
            {
                date = today,
                deletedRunners,
                deletedCompetitions,
                deletedClasses,
                deletedClubs
            });
        });
    }
}
