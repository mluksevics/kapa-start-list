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
    }
}
