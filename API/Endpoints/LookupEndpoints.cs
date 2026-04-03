using Microsoft.EntityFrameworkCore;
using StartRef.Api.Data;
using StartRef.Api.Data.Entities;
using StartRef.Api.Models;

namespace StartRef.Api.Endpoints;

public static class LookupEndpoints
{
    public static void MapLookupEndpoints(this WebApplication app)
    {
        app.MapGet("/api/lookups/{date}/classes", async (string date, AppDbContext db) =>
        {
            if (!DateOnly.TryParse(date, out var competitionDate))
                return Results.BadRequest(new { error = "Invalid date format. Use yyyy-MM-dd." });

            var items = await db.Classes
                .AsNoTracking()
                .Where(x => x.CompetitionDate == competitionDate)
                .OrderBy(x => x.Name)
                .Select(x => new LookupItemRequest(x.Id, x.Name, x.StartPlace))
                .ToListAsync();
            return Results.Ok(items);
        });

        app.MapGet("/api/lookups/{date}/clubs", async (string date, AppDbContext db) =>
        {
            if (!DateOnly.TryParse(date, out var competitionDate))
                return Results.BadRequest(new { error = "Invalid date format. Use yyyy-MM-dd." });

            var items = await db.Clubs
                .AsNoTracking()
                .Where(x => x.CompetitionDate == competitionDate)
                .OrderBy(x => x.Name)
                .Select(x => new LookupItemRequest(x.Id, x.Name, 0))
                .ToListAsync();
            return Results.Ok(items);
        });

        app.MapGet("/api/lookups/counts", async (AppDbContext db) =>
        {
            var competitors = await db.Runners.CountAsync();
            var clubs = await db.Clubs.CountAsync();
            var classes = await db.Classes.CountAsync();

            return Results.Ok(new LookupCountsResponse(competitors, clubs, classes));
        });

        app.MapGet("/api/lookups/counts/{date}", async (string date, AppDbContext db) =>
        {
            if (!DateOnly.TryParse(date, out var competitionDate))
                return Results.BadRequest(new { error = "Invalid date format. Use yyyy-MM-dd." });

            var competitors = await db.Runners
                .AsNoTracking()
                .Where(r => r.CompetitionDate == competitionDate)
                .CountAsync();
            var clubs = await db.Clubs
                .AsNoTracking()
                .Where(c => c.CompetitionDate == competitionDate)
                .CountAsync();
            var classes = await db.Classes
                .AsNoTracking()
                .Where(c => c.CompetitionDate == competitionDate)
                .CountAsync();

            return Results.Ok(new LookupCountsResponse(competitors, clubs, classes));
        });

        app.MapPut("/api/lookups/{date}/classes", async (string date, UpsertLookupRequest request, AppDbContext db) =>
        {
            if (!DateOnly.TryParse(date, out var competitionDate))
                return Results.BadRequest(new { error = "Invalid date format. Use yyyy-MM-dd." });

            if (request.Items is null || request.Items.Count == 0)
                return Results.BadRequest(new { error = "Items list cannot be empty." });

            var normalized = request.Items
                .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Name))
                .GroupBy(x => x.Id)
                .Select(g => new LookupItemRequest(g.Key, g.First().Name.Trim(), g.First().StartPlace))
                .Where(x => x.Name.Length > 0)
                .ToList();

            if (normalized.Count == 0)
                return Results.BadRequest(new { error = "No valid items (id > 0 and non-empty name)." });

            var ids = normalized.Select(x => x.Id).ToList();
            var existing = await db.Classes
                .Where(x => x.CompetitionDate == competitionDate && ids.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);

            int inserted = 0, updated = 0, unchanged = 0;
            foreach (var item in normalized)
            {
                if (existing.TryGetValue(item.Id, out var current))
                {
                    bool nameChanged = current.Name != item.Name;
                    bool placeChanged = current.StartPlace != item.StartPlace;
                    if (nameChanged)
                        current.Name = item.Name;
                    if (placeChanged)
                        current.StartPlace = item.StartPlace;
                    if (nameChanged || placeChanged)
                        updated++;
                    else
                        unchanged++;
                }
                else
                {
                    db.Classes.Add(new Class
                    {
                        CompetitionDate = competitionDate,
                        Id = item.Id,
                        Name = item.Name,
                        StartPlace = item.StartPlace
                    });
                    inserted++;
                }
            }

            await db.SaveChangesAsync();
            return Results.Ok(new UpsertLookupResponse(inserted, updated, unchanged));
        });

        app.MapPut("/api/lookups/{date}/clubs", async (string date, UpsertLookupRequest request, AppDbContext db) =>
        {
            if (!DateOnly.TryParse(date, out var competitionDate))
                return Results.BadRequest(new { error = "Invalid date format. Use yyyy-MM-dd." });

            if (request.Items is null || request.Items.Count == 0)
                return Results.BadRequest(new { error = "Items list cannot be empty." });

            var normalized = request.Items
                .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Name))
                .GroupBy(x => x.Id)
                .Select(g => new LookupItemRequest(g.Key, g.First().Name.Trim(), 0))
                .Where(x => x.Name.Length > 0)
                .ToList();

            if (normalized.Count == 0)
                return Results.BadRequest(new { error = "No valid items (id > 0 and non-empty name)." });

            var ids = normalized.Select(x => x.Id).ToList();
            var existing = await db.Clubs
                .Where(x => x.CompetitionDate == competitionDate && ids.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id);

            int inserted = 0, updated = 0, unchanged = 0;
            foreach (var item in normalized)
            {
                if (existing.TryGetValue(item.Id, out var current))
                {
                    if (current.Name != item.Name)
                    {
                        current.Name = item.Name;
                        updated++;
                    }
                    else
                    {
                        unchanged++;
                    }
                }
                else
                {
                    db.Clubs.Add(new Club
                    {
                        CompetitionDate = competitionDate,
                        Id = item.Id,
                        Name = item.Name
                    });
                    inserted++;
                }
            }

            await db.SaveChangesAsync();
            return Results.Ok(new UpsertLookupResponse(inserted, updated, unchanged));
        });
    }
}
