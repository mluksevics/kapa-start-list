using Microsoft.EntityFrameworkCore;
using StartRef.Api.Data;
using StartRef.Api.Models;

namespace StartRef.Api.Endpoints;

public static class ChangeLogEndpoints
{
    public static void MapChangeLogEndpoints(this WebApplication app)
    {
        // GET /api/competitions/{date}/changelog?max=100&startNumber=N
        app.MapGet("/api/competitions/{date}/changelog", async (
            string date,
            int? max,
            int? startNumber,
            AppDbContext db) =>
        {
            if (!DateOnly.TryParse(date, out var competitionDate))
                return Results.BadRequest(new { error = "Invalid date format. Use yyyy-MM-dd." });

            var take = Math.Clamp(max ?? 100, 1, 1000);
            var query = db.ChangeLogEntries
                .AsNoTracking()
                .Where(e => e.CompetitionDate == competitionDate);

            if (startNumber.HasValue)
                query = query.Where(e => e.StartNumber == startNumber.Value);

            var entries = await query
                .OrderByDescending(e => e.ChangedAtUtc)
                .Take(take)
                .Select(e => new ChangeLogEntryResponse(
                    e.Id,
                    e.CompetitionDate,
                    e.StartNumber,
                    e.FieldName,
                    e.OldValue,
                    e.NewValue,
                    e.ChangedAtUtc,
                    e.ChangedBy))
                .ToListAsync();

            return Results.Ok(entries);
        });
    }
}
