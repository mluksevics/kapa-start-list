using Microsoft.EntityFrameworkCore;
using StartRef.Api.Data;
using StartRef.Api.Models;

namespace StartRef.Api.Endpoints;

public static class StatusEndpoints
{
    public static void MapStatusEndpoints(this WebApplication app)
    {
        app.MapGet("/api/statuses", async (AppDbContext db) =>
        {
            var statuses = await db.Statuses
                .AsNoTracking()
                .OrderBy(s => s.Id)
                .Select(s => new StatusResponse(s.Id, s.Name))
                .ToListAsync();
            return Results.Ok(statuses);
        });
    }
}
