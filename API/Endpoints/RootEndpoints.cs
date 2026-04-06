using Microsoft.EntityFrameworkCore;
using StartRef.Api.Data;
using StartRef.Api.Models;

namespace StartRef.Api.Endpoints;

public static class RootEndpoints
{
    public static void MapRootEndpoints(this WebApplication app)
    {
        app.MapGet("/api/time", () =>
            Results.Ok(new { serverTimeUtc = DateTimeOffset.UtcNow }));

        app.MapGet("/", async (AppDbContext db) =>
        {
            var competitions = await db.Competitions
                .AsNoTracking()
                .OrderByDescending(c => c.Date)
                .Select(c => new CompetitionRunnerCountResponse(
                    c.Date,
                    c.Name,
                    c.Runners.Count))
                .ToListAsync();

            return Results.Ok(competitions);
        });
    }
}
