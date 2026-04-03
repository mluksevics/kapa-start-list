using Microsoft.EntityFrameworkCore;
using StartRef.Api.Data;
using StartRef.Api.Data.Entities;
using StartRef.Api.Models;

namespace StartRef.Api.Endpoints;

public static class RunnerEndpoints
{
    public static void MapRunnerEndpoints(this WebApplication app)
    {
        // GET /api/competitions/{date}/runners?changedSince=ISO
        app.MapGet("/api/competitions/{date}/runners", async (
            string date,
            DateTimeOffset? changedSince,
            AppDbContext db) =>
        {
            if (!DateOnly.TryParse(date, out var competitionDate))
                return Results.BadRequest(new { error = "Invalid date format. Use yyyy-MM-dd." });

            await EnsureCompetitionExistsAsync(db, competitionDate);

            var serverTimeUtc = DateTimeOffset.UtcNow;

            var classNames = await db.Classes.AsNoTracking()
                .Where(c => c.CompetitionDate == competitionDate)
                .ToDictionaryAsync(c => c.Id, c => c.Name);
            var clubNames  = await db.Clubs.AsNoTracking()
                .Where(c => c.CompetitionDate == competitionDate)
                .ToDictionaryAsync(c => c.Id, c => c.Name);

            var query = db.Runners
                .AsNoTracking()
                .Include(r => r.Status)
                .Where(r => r.CompetitionDate == competitionDate);

            if (changedSince.HasValue)
                query = query.Where(r => r.LastModifiedUtc > changedSince.Value);

            var runnerEntities = await query.OrderBy(r => r.StartNumber).ToListAsync();

            IReadOnlyDictionary<int, IReadOnlyList<string>>? changedFieldsByStart = null;
            if (changedSince.HasValue && runnerEntities.Count > 0)
            {
                var startNumbers = runnerEntities.Select(r => r.StartNumber).ToList();
                var logRows = await db.ChangeLogEntries.AsNoTracking()
                    .Where(e =>
                        e.CompetitionDate == competitionDate &&
                        e.ChangedAtUtc > changedSince.Value &&
                        startNumbers.Contains(e.StartNumber))
                    .Select(e => new { e.StartNumber, e.FieldName })
                    .ToListAsync();

                changedFieldsByStart = logRows
                    .GroupBy(e => e.StartNumber)
                    .ToDictionary(
                        g => g.Key,
                        g => (IReadOnlyList<string>)g.Select(e => e.FieldName).Distinct(StringComparer.Ordinal).ToList());
            }

            var runners = MapToRunnerResponses(runnerEntities, classNames, clubNames, changedFieldsByStart);

            return Results.Ok(new GetRunnersResponse(serverTimeUtc, runners));
        });

        // GET /api/competitions/{date}/runners/registered — status Registered (1) only
        app.MapGet("/api/competitions/{date}/runners/registered", async (string date, AppDbContext db) =>
            await GetRunnersForStatusesAsync(date, db, [1]));

        // GET /api/competitions/{date}/runners/dns — status DNS (3) only
        app.MapGet("/api/competitions/{date}/runners/dns", async (string date, AppDbContext db) =>
            await GetRunnersForStatusesAsync(date, db, [3]));

        // PUT /api/competitions/{date}/runners?touchAll=true — bulk upload (touchAll bumps every row)
        app.MapPut("/api/competitions/{date}/runners", async (
            string date,
            BulkUploadRequest request,
            AppDbContext db,
            bool touchAll = false) =>
        {
            if (!DateOnly.TryParse(date, out var competitionDate))
                return Results.BadRequest(new { error = "Invalid date format. Use yyyy-MM-dd." });

            if (request.Runners is null || request.Runners.Count == 0)
                return Results.BadRequest(new { error = "Runners list cannot be empty." });

            var utcNow = DateTimeOffset.UtcNow;

            // Auto-create competition if not exists
            var competition = await db.Competitions.FindAsync(competitionDate);
            bool competitionCreated = false;
            if (competition is null)
            {
                competition = new Competition
                {
                    Date = competitionDate,
                    CreatedAtUtc = utcNow
                };
                db.Competitions.Add(competition);
                competitionCreated = true;
            }

            // Upsert Classes — single batch query instead of per-item FindAsync
            var incomingClassGroups = request.Runners
                .Where(r => r.ClassId > 0 && !string.IsNullOrEmpty(r.ClassName))
                .GroupBy(r => r.ClassId)
                .Select(g => new { Id = g.Key, Name = g.First().ClassName })
                .ToList();

            if (incomingClassGroups.Count > 0)
            {
                var classIds = incomingClassGroups.Select(c => c.Id).ToList();
                var existingClasses = await db.Classes
                    .Where(c => c.CompetitionDate == competitionDate && classIds.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id);

                foreach (var cls in incomingClassGroups)
                {
                    if (existingClasses.TryGetValue(cls.Id, out var existing))
                    {
                        if (existing.Name != cls.Name)
                            existing.Name = cls.Name;
                    }
                    else
                    {
                        db.Classes.Add(new Class { CompetitionDate = competitionDate, Id = cls.Id, Name = cls.Name, StartPlace = 0 });
                    }
                }
            }

            // Upsert Clubs — single batch query
            var incomingClubGroups = request.Runners
                .Where(r => r.ClubId > 0 && !string.IsNullOrEmpty(r.ClubName))
                .GroupBy(r => r.ClubId)
                .Select(g => new { Id = g.Key, Name = g.First().ClubName })
                .ToList();

            if (incomingClubGroups.Count > 0)
            {
                var clubIds = incomingClubGroups.Select(c => c.Id).ToList();
                var existingClubs = await db.Clubs
                    .Where(c => c.CompetitionDate == competitionDate && clubIds.Contains(c.Id))
                    .ToDictionaryAsync(c => c.Id);

                foreach (var club in incomingClubGroups)
                {
                    if (existingClubs.TryGetValue(club.Id, out var existing))
                    {
                        if (existing.Name != club.Name)
                            existing.Name = club.Name;
                    }
                    else
                    {
                        db.Clubs.Add(new Club { CompetitionDate = competitionDate, Id = club.Id, Name = club.Name });
                    }
                }
            }

            // Load only the runners we need (by incoming start numbers) instead of the entire table
            var incomingStartNumbers = request.Runners.Select(r => r.StartNumber).ToList();
            var existingRunners = await db.Runners
                .Where(r => r.CompetitionDate == competitionDate && incomingStartNumbers.Contains(r.StartNumber))
                .ToDictionaryAsync(r => r.StartNumber);

            int inserted = 0, updated = 0, unchanged = 0, skippedAsOlder = 0;
            var changeLogEntries = new List<ChangeLogEntry>();

            foreach (var dto in request.Runners)
            {
                TimeOnly? startTime = TimeOnly.TryParse(dto.StartTime, out var st) ? st : null;

                if (existingRunners.TryGetValue(dto.StartNumber, out var existing))
                {
                    bool anyChange = false;
                    bool thisRunnerSkipped = false;

                    if (touchAll)
                    {
                        if (existing.SiChipNo != dto.SiChipNo)
                        {
                            changeLogEntries.Add(MakeLog(competitionDate, dto.StartNumber, "SiChipNo", existing.SiChipNo, dto.SiChipNo, utcNow, request.Source));
                            existing.SiChipNo = dto.SiChipNo;
                            anyChange = true;
                        }
                        if (existing.Name != dto.Name)
                        {
                            changeLogEntries.Add(MakeLog(competitionDate, dto.StartNumber, "Name", existing.Name, dto.Name, utcNow, request.Source));
                            existing.Name = dto.Name;
                            anyChange = true;
                        }
                        if (existing.Surname != dto.Surname)
                        {
                            changeLogEntries.Add(MakeLog(competitionDate, dto.StartNumber, "Surname", existing.Surname, dto.Surname, utcNow, request.Source));
                            existing.Surname = dto.Surname;
                            anyChange = true;
                        }
                        if (existing.ClassId != dto.ClassId)
                        {
                            changeLogEntries.Add(MakeLog(competitionDate, dto.StartNumber, "ClassId", existing.ClassId.ToString(), dto.ClassId.ToString(), utcNow, request.Source));
                            existing.ClassId = dto.ClassId;
                            anyChange = true;
                        }
                        if (existing.ClubId != dto.ClubId)
                        {
                            changeLogEntries.Add(MakeLog(competitionDate, dto.StartNumber, "ClubId", existing.ClubId.ToString(), dto.ClubId.ToString(), utcNow, request.Source));
                            existing.ClubId = dto.ClubId;
                            anyChange = true;
                        }
                        if (startTime.HasValue && existing.StartTime != startTime)
                        {
                            changeLogEntries.Add(MakeLog(competitionDate, dto.StartNumber, "StartTime", existing.StartTime?.ToString("HH:mm:ss"), dto.StartTime, utcNow, request.Source));
                            existing.StartTime = startTime;
                            anyChange = true;
                        }

                        int resolvedTouch = ResolveStatusForBulkUpload(incoming: dto.StatusId, current: existing.StatusId);
                        if (resolvedTouch != existing.StatusId)
                        {
                            changeLogEntries.Add(MakeLog(competitionDate, dto.StartNumber, "StatusId", existing.StatusId.ToString(), resolvedTouch.ToString(), utcNow, request.Source));
                            existing.StatusId = resolvedTouch;
                            anyChange = true;
                        }

                        if (anyChange)
                        {
                            existing.LastModifiedUtc = utcNow;
                            existing.LastModifiedBy = request.Source;
                            updated++;
                        }
                        else
                        {
                            unchanged++;
                        }
                    }
                    else
                    {
                        // Non-status fields: per-runner timestamp wins over server timestamp.
                        bool incomingIsNewer = dto.LastModifiedUtc >= existing.LastModifiedUtc;

                        if (incomingIsNewer)
                        {
                            if (existing.SiChipNo != dto.SiChipNo)
                            {
                                changeLogEntries.Add(MakeLog(competitionDate, dto.StartNumber, "SiChipNo", existing.SiChipNo, dto.SiChipNo, utcNow, request.Source));
                                existing.SiChipNo = dto.SiChipNo;
                                anyChange = true;
                            }
                            if (existing.Name != dto.Name)
                            {
                                changeLogEntries.Add(MakeLog(competitionDate, dto.StartNumber, "Name", existing.Name, dto.Name, utcNow, request.Source));
                                existing.Name = dto.Name;
                                anyChange = true;
                            }
                            if (existing.Surname != dto.Surname)
                            {
                                changeLogEntries.Add(MakeLog(competitionDate, dto.StartNumber, "Surname", existing.Surname, dto.Surname, utcNow, request.Source));
                                existing.Surname = dto.Surname;
                                anyChange = true;
                            }
                            if (existing.ClassId != dto.ClassId)
                            {
                                changeLogEntries.Add(MakeLog(competitionDate, dto.StartNumber, "ClassId", existing.ClassId.ToString(), dto.ClassId.ToString(), utcNow, request.Source));
                                existing.ClassId = dto.ClassId;
                                anyChange = true;
                            }
                            if (existing.ClubId != dto.ClubId)
                            {
                                changeLogEntries.Add(MakeLog(competitionDate, dto.StartNumber, "ClubId", existing.ClubId.ToString(), dto.ClubId.ToString(), utcNow, request.Source));
                                existing.ClubId = dto.ClubId;
                                anyChange = true;
                            }
                            if (startTime.HasValue && existing.StartTime != startTime)
                            {
                                changeLogEntries.Add(MakeLog(competitionDate, dto.StartNumber, "StartTime", existing.StartTime?.ToString("HH:mm:ss"), dto.StartTime, utcNow, request.Source));
                                existing.StartTime = startTime;
                                anyChange = true;
                            }
                        }
                        else
                        {
                            thisRunnerSkipped = true;
                        }

                        int resolvedStatus = ResolveStatusForBulkUpload(incoming: dto.StatusId, current: existing.StatusId);
                        if (resolvedStatus != existing.StatusId)
                        {
                            changeLogEntries.Add(MakeLog(competitionDate, dto.StartNumber, "StatusId", existing.StatusId.ToString(), resolvedStatus.ToString(), utcNow, request.Source));
                            existing.StatusId = resolvedStatus;
                            anyChange = true;
                        }

                        if (anyChange)
                        {
                            existing.LastModifiedUtc = utcNow;
                            existing.LastModifiedBy = request.Source;
                            updated++;
                        }
                        else if (thisRunnerSkipped)
                        {
                            skippedAsOlder++;
                        }
                        else
                        {
                            unchanged++;
                        }
                    }
                }
                else
                {
                    // New runner — INSERT
                    var runner = new Runner
                    {
                        CompetitionDate = competitionDate,
                        StartNumber = dto.StartNumber,
                        SiChipNo = dto.SiChipNo,
                        Name = dto.Name,
                        Surname = dto.Surname,
                        ClassId = dto.ClassId,
                        ClubId = dto.ClubId,
                        StatusId = dto.StatusId,
                        StartTime = startTime,
                        LastModifiedUtc = utcNow,
                        LastModifiedBy = request.Source
                    };
                    db.Runners.Add(runner);

                    changeLogEntries.Add(MakeLog(competitionDate, dto.StartNumber, "Name", null, dto.Name, utcNow, request.Source));
                    changeLogEntries.Add(MakeLog(competitionDate, dto.StartNumber, "Surname", null, dto.Surname, utcNow, request.Source));
                    changeLogEntries.Add(MakeLog(competitionDate, dto.StartNumber, "ClassId", null, dto.ClassId.ToString(), utcNow, request.Source));
                    changeLogEntries.Add(MakeLog(competitionDate, dto.StartNumber, "ClubId", null, dto.ClubId.ToString(), utcNow, request.Source));
                    if (dto.SiChipNo is not null)
                        changeLogEntries.Add(MakeLog(competitionDate, dto.StartNumber, "SiChipNo", null, dto.SiChipNo, utcNow, request.Source));
                    if (dto.StatusId != 1)
                        changeLogEntries.Add(MakeLog(competitionDate, dto.StartNumber, "StatusId", "1", dto.StatusId.ToString(), utcNow, request.Source));
                    if (startTime.HasValue)
                        changeLogEntries.Add(MakeLog(competitionDate, dto.StartNumber, "StartTime", null, dto.StartTime, utcNow, request.Source));

                    inserted++;
                }
            }

            if (changeLogEntries.Count > 0)
                db.ChangeLogEntries.AddRange(changeLogEntries);

            await db.SaveChangesAsync();

            return Results.Ok(new BulkUploadResponse(
                competitionDate,
                competitionCreated,
                inserted,
                updated,
                unchanged,
                skippedAsOlder));
        });

        // PATCH /api/competitions/{date}/runners/{startNumber}
        app.MapMethods("/api/competitions/{date}/runners/{startNumber}", ["PATCH"], async (
            string date,
            int startNumber,
            PatchRunnerRequest request,
            AppDbContext db) =>
        {
            if (!DateOnly.TryParse(date, out var competitionDate))
                return Results.BadRequest(new { error = "Invalid date format. Use yyyy-MM-dd." });

            var runner = await db.Runners.FindAsync(competitionDate, startNumber);
            if (runner is null)
                return Results.NotFound(new { error = $"Runner {startNumber} not found for {date}." });

            // Last-write-wins: reject if incoming is older than server
            if (request.LastModifiedUtc < runner.LastModifiedUtc)
            {
                return Results.Conflict(new PatchRunnerResponse(
                    startNumber,
                    Applied: false,
                    Reason: "Server has a newer version of this runner.",
                    ServerLastModifiedUtc: runner.LastModifiedUtc));
            }

            var utcNow = DateTimeOffset.UtcNow;
            var changeLogEntries = new List<ChangeLogEntry>();
            bool anyChange = false;

            if (request.StatusId.HasValue)
            {
                int v = request.StatusId.Value;
                if (v is >= 1 and <= 3 && v != runner.StatusId)
                {
                    changeLogEntries.Add(MakeLog(competitionDate, startNumber, "StatusId", runner.StatusId.ToString(), v.ToString(), utcNow, request.Source));
                    runner.StatusId = v;
                    anyChange = true;
                }
            }

            if (request.SiChipNo is not null && request.SiChipNo != runner.SiChipNo)
            {
                changeLogEntries.Add(MakeLog(competitionDate, startNumber, "SiChipNo", runner.SiChipNo, request.SiChipNo, utcNow, request.Source));
                runner.SiChipNo = request.SiChipNo;
                anyChange = true;
            }

            if (request.Name is not null && request.Name != runner.Name)
            {
                changeLogEntries.Add(MakeLog(competitionDate, startNumber, "Name", runner.Name, request.Name, utcNow, request.Source));
                runner.Name = request.Name;
                anyChange = true;
            }

            if (request.Surname is not null && request.Surname != runner.Surname)
            {
                changeLogEntries.Add(MakeLog(competitionDate, startNumber, "Surname", runner.Surname, request.Surname, utcNow, request.Source));
                runner.Surname = request.Surname;
                anyChange = true;
            }

            if (request.ClassId.HasValue && request.ClassId.Value != runner.ClassId)
            {
                changeLogEntries.Add(MakeLog(competitionDate, startNumber, "ClassId", runner.ClassId.ToString(), request.ClassId.Value.ToString(), utcNow, request.Source));
                runner.ClassId = request.ClassId.Value;
                anyChange = true;
            }

            if (request.ClubId.HasValue && request.ClubId.Value != runner.ClubId)
            {
                changeLogEntries.Add(MakeLog(competitionDate, startNumber, "ClubId", runner.ClubId.ToString(), request.ClubId.Value.ToString(), utcNow, request.Source));
                runner.ClubId = request.ClubId.Value;
                anyChange = true;
            }

            if (request.StartTime is not null &&
                TimeOnly.TryParse(request.StartTime, out var newStartTime) &&
                runner.StartTime != newStartTime)
            {
                changeLogEntries.Add(MakeLog(competitionDate, startNumber, "StartTime", runner.StartTime?.ToString("HH:mm:ss"), request.StartTime, utcNow, request.Source));
                runner.StartTime = newStartTime;
                anyChange = true;
            }

            if (anyChange)
            {
                runner.LastModifiedUtc = utcNow;
                runner.LastModifiedBy = request.Source;
                db.ChangeLogEntries.AddRange(changeLogEntries);
                await db.SaveChangesAsync();
            }

            return Results.Ok(new PatchRunnerResponse(
                startNumber,
                Applied: anyChange,
                Reason: anyChange ? null : "No fields changed.",
                ServerLastModifiedUtc: runner.LastModifiedUtc));
        });

        // DELETE /api/competitions/{date}/runners — clear startlist for a date
        app.MapDelete("/api/competitions/{date}/runners", async (string date, AppDbContext db) =>
        {
            if (!DateOnly.TryParse(date, out var competitionDate))
                return Results.BadRequest(new { error = "Invalid date format. Use yyyy-MM-dd." });

            var deleted = await db.Runners
                .Where(r => r.CompetitionDate == competitionDate)
                .ExecuteDeleteAsync();

            return Results.Ok(new { deleted });
        });
    }

    private static async Task EnsureCompetitionExistsAsync(AppDbContext db, DateOnly competitionDate)
    {
        var competition = await db.Competitions.FindAsync(competitionDate);
        if (competition is null)
        {
            db.Competitions.Add(new Competition
            {
                Date = competitionDate,
                CreatedAtUtc = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }
    }

    private static List<RunnerResponse> MapToRunnerResponses(
        List<Runner> runnerEntities,
        IReadOnlyDictionary<int, string> classNames,
        IReadOnlyDictionary<int, string> clubNames,
        IReadOnlyDictionary<int, IReadOnlyList<string>>? changedFieldsByStart)
    {
        return runnerEntities
            .Select(r => new RunnerResponse(
                r.CompetitionDate,
                r.StartNumber,
                r.SiChipNo,
                r.Name,
                r.Surname,
                r.ClassId,
                classNames.GetValueOrDefault(r.ClassId, ""),
                r.ClubId,
                clubNames.GetValueOrDefault(r.ClubId, ""),
                r.StatusId,
                r.Status.Name,
                r.StartTime?.ToString("HH:mm:ss"),
                r.LastModifiedUtc,
                r.LastModifiedBy,
                changedFieldsByStart is not null && changedFieldsByStart.TryGetValue(r.StartNumber, out var fields)
                    ? fields
                    : null))
            .ToList();
    }

    private static async Task<IResult> GetRunnersForStatusesAsync(string date, AppDbContext db, int[] statusIds)
    {
        if (!DateOnly.TryParse(date, out var competitionDate))
            return Results.BadRequest(new { error = "Invalid date format. Use yyyy-MM-dd." });

        await EnsureCompetitionExistsAsync(db, competitionDate);

        var serverTimeUtc = DateTimeOffset.UtcNow;
        var classNames = await db.Classes.AsNoTracking()
            .Where(c => c.CompetitionDate == competitionDate)
            .ToDictionaryAsync(c => c.Id, c => c.Name);
        var clubNames  = await db.Clubs.AsNoTracking()
            .Where(c => c.CompetitionDate == competitionDate)
            .ToDictionaryAsync(c => c.Id, c => c.Name);

        var runnerEntities = await db.Runners
            .AsNoTracking()
            .Include(r => r.Status)
            .Where(r => r.CompetitionDate == competitionDate && statusIds.Contains(r.StatusId))
            .OrderBy(r => r.StartNumber)
            .ToListAsync();

        var runners = MapToRunnerResponses(runnerEntities, classNames, clubNames, changedFieldsByStart: null);
        return Results.Ok(new GetRunnersResponse(serverTimeUtc, runners));
    }

    /// <summary>
    /// Bulk upload (desktop/OE12): forward-only — never downgrade Started/DNS to Registered so a full push does not erase field device state.
    /// PATCH from Android applies the requested status directly (including Registered/Cleared).
    /// </summary>
    private static int ResolveStatusForBulkUpload(int incoming, int current)
    {
        if (incoming == 1 && current is 2 or 3)
            return current;

        if (incoming == 3)
            return 3;

        if (incoming == 2 && current == 1)
            return 2;

        return current;
    }

    private static ChangeLogEntry MakeLog(
        DateOnly date, int startNumber, string field,
        string? oldVal, string? newVal,
        DateTimeOffset changedAt, string changedBy) => new()
    {
        CompetitionDate = date,
        StartNumber = startNumber,
        FieldName = field,
        OldValue = oldVal,
        NewValue = newVal,
        ChangedAtUtc = changedAt,
        ChangedBy = changedBy
    };
}
