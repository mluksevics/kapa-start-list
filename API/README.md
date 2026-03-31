# StartRef API

.NET 8 minimal API serving as the **central source of truth** for the StartRef orienteering start-list management system. Backed by Azure SQL Server; consumed by the Android Referee app and the Windows Desktop sync tool.

---

## Endpoints

All mutation endpoints require the `X-Api-Key` header. GET endpoints are open.

### Runners

| Method | Path | Description |
|--------|------|-------------|
| `PUT` | `/api/competitions/{date}/runners` | Bulk upload from Desktop (upsert with status rules) |
| `PATCH` | `/api/competitions/{date}/runners/{startNumber}` | Partial update from Android |
| `GET` | `/api/competitions/{date}/runners[?changedSince=ISO]` | Full or delta fetch; returns `serverTimeUtc` and runner rows (see `changedFields` below) |
| `DELETE` | `/api/competitions/{date}/runners` | Clear all runners for a date |

**GET runners — `changedFields` (delta pulls only):** When `changedSince` is present, each runner object may include `changedFields`: a string array of logical column names (`SiChipNo`, `Name`, `Surname`, `ClassId`, `ClubId`, `Country`, `StartPlace`, `StartTime`, `StatusId`) that had a changelog entry with `ChangedAtUtc` after the watermark. Names match `PATCH` / bulk upload audit log field names. If a runner appears in the delta but has no matching changelog rows in that window, `changedFields` is omitted (clients fall back to comparing full row values). When `changedSince` is omitted (full fetch), `changedFields` is omitted.

### Reference data

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/competitions` | List competitions, newest first |
| `GET` | `/api/competitions/{date}/changelog[?max=N&startNumber=N]` | Audit log, newest first |
| `GET` | `/api/statuses` | Status lookup (1=Registered, 2=Started, 3=DNS) |

---

## Data model

```
Status        (id, name)                              — seed data, read-only
Competition   (date PK, name?, createdAtUtc)          — auto-created on first bulk upload
Runner        (competitionDate+startNumber PK, …, statusId FK, lastModifiedUtc, lastModifiedBy)
ChangeLogEntry (id identity, competitionDate, startNumber, fieldName, oldValue, newValue, changedAtUtc, changedBy)
```

`className` is **immutable** after a runner is first inserted — it is silently ignored in PATCH and not overwritten in bulk uploads.

---

## Status rules

### `PATCH` (Android / referee)

Any valid `statusId` 1–3 is stored as sent (including **Started/DNS → Registered**), subject to `lastModifiedUtc` conflict rules. Referee corrections propagate to the API and other clients on pull.

### `PUT` bulk upload (Desktop / OE12 scan)

Forward-only so a full push (where scans often send Registered) does not clear field-device statuses:

| Incoming | Current | Result |
|----------|---------|--------|
| Registered (1) | Started (2) | Keep Started |
| Registered (1) | DNS (3) | Keep DNS |
| Started (2) | Registered (1) | Apply Started |
| DNS (3) | Registered (1) | Apply DNS |
| DNS (3) | Started (2) | Apply DNS |

---

## Bulk upload conflict resolution (`PUT`)

For non-status fields: incoming `lastModifiedUtc` is compared to server `LastModifiedUtc`.
- Incoming ≥ server → apply the field
- Incoming < server → skip the field (`skippedAsOlder` counter)

**Force Push All** (Desktop): sends `lastModifiedUtc = DateTimeOffset.UtcNow`, guaranteeing non-status fields overwrite server values. **Bulk status rules** above still apply so runner state from gates/phones is not reset to Registered.

---

## Configuration

```jsonc
// appsettings.json (production)
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=tcp:<server>.database.windows.net,1433;Initial Catalog=startref;..."
  },
  "ApiKey": "replace-with-strong-api-key"
}

// appsettings.Development.json (local)
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=(localdb)\\mssqllocaldb;Database=startref_dev;Trusted_Connection=True;"
  },
  "ApiKey": "dev-local-api-key"
}
```

---

## Running locally

```bash
cd API
dotnet run
```

The database is created and migrated automatically on startup (`MigrateAsync()`). Status seed data (Registered/Started/DNS) is applied via `HasData` in the migration.

To regenerate migrations after model changes:

```bash
cd API
dotnet ef migrations add <MigrationName>
```

---

## Stack

- .NET 8 minimal API
- Entity Framework Core 8 + SQL Server provider
- `Microsoft.AspNetCore.ResponseCompression` — gzip + brotli (enabled for HTTPS)
- `ApiKeyAuthMiddleware` — checks `X-Api-Key` header on all non-GET requests

---

## Project layout

```
API/
  Data/
    AppDbContext.cs
    Entities/           Competition, Runner, Status, ChangeLogEntry
    Migrations/         EF Core migrations (SQL Server)
  Endpoints/
    RunnerEndpoints.cs
    CompetitionEndpoints.cs
    ChangeLogEndpoints.cs
    StatusEndpoints.cs
  Models/               Request/response DTOs
  ApiKeyAuthMiddleware.cs
  Program.cs
```
