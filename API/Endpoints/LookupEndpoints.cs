using Microsoft.EntityFrameworkCore;
using StartRef.Api.Data;
using StartRef.Api.Data.Entities;
using StartRef.Api.Models;

namespace StartRef.Api.Endpoints;

public static class LookupEndpoints
{
    public static void MapLookupEndpoints(this WebApplication app)
    {
        app.MapGet("/api/lookups/classes", async (AppDbContext db) =>
        {
            var items = await db.Classes
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new LookupItemRequest(x.Id, x.Name))
                .ToListAsync();
            return Results.Ok(items);
        });

        app.MapGet("/api/lookups/clubs", async (AppDbContext db) =>
        {
            var items = await db.Clubs
                .AsNoTracking()
                .OrderBy(x => x.Name)
                .Select(x => new LookupItemRequest(x.Id, x.Name))
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

        app.MapPut("/api/lookups/classes", async (UpsertLookupRequest request, AppDbContext db) =>
        {
            if (request.Items is null || request.Items.Count == 0)
                return Results.BadRequest(new { error = "Items list cannot be empty." });

            var normalized = request.Items
                .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Name))
                .GroupBy(x => x.Id)
                .Select(g => new LookupItemRequest(g.Key, g.First().Name.Trim()))
                .Where(x => x.Name.Length > 0)
                .ToList();

            if (normalized.Count == 0)
                return Results.BadRequest(new { error = "No valid items (id > 0 and non-empty name)." });

            var ids = normalized.Select(x => x.Id).ToList();
            var existing = await db.Classes
                .Where(x => ids.Contains(x.Id))
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
                    db.Classes.Add(new Class
                    {
                        Id = item.Id,
                        Name = item.Name
                    });
                    inserted++;
                }
            }

            await db.SaveChangesAsync();
            return Results.Ok(new UpsertLookupResponse(inserted, updated, unchanged));
        });

        app.MapPut("/api/lookups/clubs", async (UpsertLookupRequest request, AppDbContext db) =>
        {
            if (request.Items is null || request.Items.Count == 0)
                return Results.BadRequest(new { error = "Items list cannot be empty." });

            var normalized = request.Items
                .Where(x => x.Id > 0 && !string.IsNullOrWhiteSpace(x.Name))
                .GroupBy(x => x.Id)
                .Select(g => new LookupItemRequest(g.Key, g.First().Name.Trim()))
                .Where(x => x.Name.Length > 0)
                .ToList();

            if (normalized.Count == 0)
                return Results.BadRequest(new { error = "No valid items (id > 0 and non-empty name)." });

            var ids = normalized.Select(x => x.Id).ToList();
            var existing = await db.Clubs
                .Where(x => ids.Contains(x.Id))
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
