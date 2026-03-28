# Architecture and technology for orienteering API
_Exported on 3/28/2026 at 13:18:15 GMT+2 from Cursor (2.6.19)_

---

**User**

I am building a .NET code API that will be used by multiple mobile devices and single desktop app.  On desktop computer there will be DB that is updated through desktop APP. DB will also be directly updated from other apps on desktop. This is all to manage a startlist changes in orienteering sports competitions.
I want to keep the data in all devices up to sync, even through there may be a poor/limited internet connection in the wilderness areas. 
Data I am interested about each runner is - start number (unique identifier), SI chip no, Name, Surname, Class name, Club name, Status (started, Did Not Start), Country, startPlace (number).

Please evaluate my idea and suggest alternatives for architecture,tech:
- .NET core app hosted as "app service". 
- updates FROM each Android device:
	- posted to my API
	- saved in local DB (sqlite)
	- available to see in API output as a long list (latest first). This enables to oversee data
	- pushed to Azure Service Bus
	- Desktop app pulls messages from service bus, updates one-by-one
- updates TO each device:
	- remember that there can also be updates directly in DB, so just sharing updates between devices are not sufficient.
	- can be downloaded in one go (gzip HTTP request contents?) from API - as entire update to startlist. This should be manually triggered from device.
	- desktop app uploads data from DB automatically at intervals.
- Each day (keep UTC) will be a new competition with new startlist - you can keep the previous data, but next day should have no data unless specifically uploaded. 

So in in this repo you can see Andoid Gate and Android Referree as mobile apps, empty API for .NET api, and Desktop for new Windows Forms app that will interact with database

---

**Cursor**

Let me first explore the repo structure to understand what's already in place.

Now I have a clear picture of your codebase and architecture. Here's my evaluation.

---

## What You've Built So Far

Your API is essentially a **custom message broker** — post messages, pull-and-lock, ack/fail with retry semantics. The Android app does offline-first with Room DB and queues changes as JSON messages. This is solid foundational work.

## Evaluation of Your Proposed Architecture

### What works well

- **Offline-first on Android** with local SQLite + pending sync queue — exactly right for wilderness conditions
- **API as central observability hub** (browsable message log, latest-first) — useful for debugging on competition day
- **Full-list download as manual trigger** — pragmatic for low-bandwidth; a typical startlist (200–2000 runners) is ~50–200KB, trivially gzippable to ~10–30KB
- **Daily competition boundary** — clean partitioning

### Issues to address

**1. Azure Service Bus is redundant — you already built the queue**

Your API already implements pull/lock/ack/fail with TTL-based lock expiry. Adding Service Bus means:
- Extra Azure cost (~$0.05/1M operations, but still)
- Two queue systems to debug
- Another credentials/connection to manage from the desktop
- The desktop can just call `GET /messages/pending?consumerId=desktop` on your existing API

**2. SQLite on Azure App Service is fragile**

App Service's filesystem is a network-mounted share (except for certain premium tiers). SQLite requires exclusive file locks that don't work reliably on network storage. You'll hit `SQLITE_BUSY` or corruption under concurrent access. Options:
- **Azure SQL Database** (serverless tier starts at ~$5/month, auto-pauses when idle)
- **PostgreSQL Flexible Server** (burstable B1ms ~$13/month)
- **SQLite on App Service** only works if you use a single instance with local SSD (P1v2+), which is expensive for this use case

**3. No conflict resolution strategy**

Two referees at Gate A and Gate B both edit the same runner's SI chip number. Who wins? You need to decide:
- **Last-write-wins** (simplest — use `lastModifiedUtc` timestamp, latest wins)
- **Field-level merge** (more complex but better — only overwrite fields that changed)
- For status fields (Started/DNS), last-write-wins with timestamps is probably fine since these are typically set at a single physical gate

**4. Desktop-as-gateway is a single point of failure**

In your design, the desktop must be running and online for changes to flow from "other apps that write to DB" into the sync loop. If the desktop crashes or loses connectivity, the system stalls. The API should be the source of truth, not the desktop's DB.

**5. Missing delta sync**

"Download entire list" is fine as a manual fallback, but for automatic sync you want: "give me everything that changed since my last sync." Without this, you're either downloading everything every time or relying solely on individual messages (which can get lost/reordered).

---

## Recommended Architecture (Simplified)

Drop Service Bus. Make the API the **single source of truth** instead of the desktop DB.

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│  Android #1  │     │  Android #2  │     │  Android #N  │
│  (Gate Ref)  │     │  (Start Ref) │     │              │
└──────┬───────┘     └──────┬───────┘     └──────┬───────┘
       │ POST /runners/:id  │                     │
       │ GET  /runners      │                     │
       ▼                    ▼                     ▼
┌─────────────────────────────────────────────────────────┐
│                    .NET API (App Service)                │
│                                                         │
│  ┌─────────────┐  ┌──────────────┐  ┌────────────────┐ │
│  │  Runners    │  │  ChangeLog   │  │  Competitions  │ │
│  │  (current)  │  │  (audit/sync)│  │  (daily)       │ │
│  └─────────────┘  └──────────────┘  └────────────────┘ │
│                                                         │
│  Azure SQL Serverless / PostgreSQL Flexible             │
└──────────────────────────┬──────────────────────────────┘
                           │
       ┌───────────────────┴───────────────────┐
       ▼                                       ▼
┌──────────────┐                    ┌──────────────────┐
│  Desktop App │                    │  DB Watcher Svc  │
│  (WinForms)  │◄───────────────────│  (Windows Svc)   │
│  GET/POST    │                    │  polls local DB, │
│  same as     │                    │  POSTs changes   │
│  Android     │                    │  to API          │
└──────────────┘                    └──────────────────┘
       │                                       ▲
       ▼                                       │
┌──────────────────────────────────────────────┐
│         Local Competition DB (desktop)        │
│  (updated by OE12/MeOS/other timing sw)       │
└───────────────────────────────────────────────┘
```

### Key API Endpoints

| Endpoint | Purpose |
|---|---|
| `POST /competitions/{date}/runners/{startNumber}` | Upsert a single runner (from any client) |
| `GET /competitions/{date}/runners` | Full startlist for today |
| `GET /competitions/{date}/runners?changedSince={iso}` | Delta sync — only rows modified after timestamp |
| `GET /competitions/{date}/changelog` | Audit trail (your existing message log, repurposed) |
| `POST /competitions/{date}/bulk-upload` | Desktop uploads full startlist from local DB |

### Runner table in API database

```csharp
public class Runner
{
    public int StartNumber { get; set; }       // PK (within competition)
    public DateOnly CompetitionDate { get; set; } // PK (partition)
    public string SiChipNo { get; set; }
    public string Name { get; set; }
    public string Surname { get; set; }
    public string ClassName { get; set; }
    public string ClubName { get; set; }
    public string Country { get; set; }
    public int StartPlace { get; set; }
    public string Status { get; set; }         // "Registered", "Started", "DNS"
    public DateTimeOffset LastModifiedUtc { get; set; }
    public string LastModifiedBy { get; set; } // "android-gate1", "desktop", etc.
}
```

### How sync works

**Android → API (push changes):**
1. User taps "Started" or edits a runner → save locally + queue a `POST /runners/{startNumber}` with the changed fields
2. If online, send immediately. If offline, `PendingSyncWorker` retries
3. API applies last-write-wins using `LastModifiedUtc`

**API → Android (pull changes):**
1. On manual refresh: `GET /runners?changedSince={lastSyncTimestamp}` → merge into local Room DB
2. Optionally: periodic background poll every 30–60s when on WiFi

**Desktop "other apps write to DB" scenario:**
1. A lightweight Windows Service or background task polls the local competition DB every 5–10 seconds
2. Detects changed rows (by comparing a hash/timestamp column)
3. POSTs them to the API via `POST /bulk-upload` or individual `POST /runners/{id}`
4. Desktop WinForms app can also `GET /runners` to see changes from the field

### Why this is simpler

- **One queue system** (your existing ChangeLog table replaces both your current message broker AND Service Bus)
- **One source of truth** (API database, not desktop DB)
- **One protocol** (HTTP REST, same for all clients)
- **No Service Bus cost or config**
- **Delta sync** avoids full downloads in the normal case; full download remains as fallback

---

## Optional Enhancements (in priority order)

1. **SignalR for real-time push** — Add a SignalR hub to the API. When a runner is updated, broadcast to all connected clients. Clients that were offline catch up via `changedSince` on reconnect. SignalR handles reconnection gracefully. This eliminates polling.

2. **ETag / `304 Not Modified`** — On the full-list endpoint, return an ETag based on the max `LastModifiedUtc`. If nothing changed, the client gets a `304` with zero body — saves bandwidth in the wilderness.

3. **Response compression middleware** — ASP.NET Core has `app.UseResponseCompression()` built-in. Handles gzip/brotli transparently based on `Accept-Encoding`. No custom implementation needed.

4. **Competition ID instead of date** — If you ever need multiple competitions on the same day (e.g., sprint in the morning, middle in the afternoon), use a GUID or slug as the competition identifier with date as a secondary field.

---

## Questions Before You Start Building

A few things that would shape implementation:

1. **What software manages the desktop DB?** (OE12, MeOS, custom Access DB?) — This determines how the "DB watcher" reads changes.
2. **How many concurrent Android devices?** 2–5 or 10+? — Affects whether you need SignalR or polling is fine.
3. **Does the desktop app need to show real-time updates from the field, or is periodic refresh fine?**
4. **What's the DB format the desktop "other apps" write to?** SQL Server, Access, SQLite, CSV export?

These answers would help finalize whether to keep polling or add SignalR, and how to design the DB watcher component.

---

**User**

1. Ok, I agree.
2. I agree, lets use Azure SQL Database
3. will use last-write-wins. But only sent changed fields from devices and only update those respectively. 
4. I can't change it. local DB is a custom product and there is entire separate system dependent on it.
5. yes, I need delta sync, add to plan.

I only need one desktop app - DB Watcher. But it should be windows forms to have a decent UI. And there should be button to to be able to post changes. I will get to that app in more details ,but lets first clarify the architecture and contracts

---

**Cursor**

Good, let me refine the architecture with those decisions locked in and define the contracts precisely.

---

## Refined Architecture

```
┌──────────────┐     ┌──────────────┐     ┌──────────────┐
│  Android #1  │     │  Android #2  │     │  Android #N  │
│  (Referee)   │     │  (Gate)      │     │              │
└──────┬───────┘     └──────┬───────┘     └──────┬───────┘
       │ PATCH /runners/:id │                     │
       │ GET  /runners      │                     │
       ▼                    ▼                     ▼
┌─────────────────────────────────────────────────────────┐
│                .NET 8 API  (App Service)                 │
│                                                         │
│  ┌─────────────┐  ┌──────────────┐  ┌────────────────┐ │
│  │  Runners    │  │  ChangeLog   │  │  Competitions  │ │
│  │  (current   │  │  (audit +    │  │  (one per day) │ │
│  │   state)    │  │   debug)     │  │                │ │
│  └─────────────┘  └──────────────┘  └────────────────┘ │
│                                                         │
│                   Azure SQL Database                    │
└──────────────────────────┬──────────────────────────────┘
                           │
                           ▼
                ┌──────────────────┐
                │  Desktop App     │
                │  (WinForms)      │
                │                  │
                │  • Watches local │
                │    competition   │
                │    DB for changes│
                │  • Manual push   │
                │    button        │
                │  • Auto-push at  │
                │    intervals     │
                │  • Shows sync    │
                │    status        │
                └────────┬─────────┘
                         │ reads
                         ▼
                ┌──────────────────┐
                │  Local DB        │
                │  (custom product,│
                │   read-only for  │
                │   our app)       │
                └──────────────────┘
```

Key principle: **the API database is the single source of truth**. All clients read from it and write to it. The local competition DB is an external input that the Desktop app watches and forwards.

---

## Data Model (Azure SQL)

### Competitions

| Column | Type | Notes |
|---|---|---|
| `Date` | `date` | PK. `2026-03-28` |
| `Name` | `nvarchar(200)` | Optional display name |
| `CreatedAtUtc` | `datetimeoffset` | |

### Runners

| Column | Type | Notes |
|---|---|---|
| `CompetitionDate` | `date` | PK (composite) |
| `StartNumber` | `int` | PK (composite) |
| `SiChipNo` | `nvarchar(20)` | Nullable |
| `Name` | `nvarchar(100)` | |
| `Surname` | `nvarchar(100)` | |
| `ClassName` | `nvarchar(50)` | |
| `ClubName` | `nvarchar(100)` | |
| `Country` | `nvarchar(10)` | ISO code |
| `Status` | `nvarchar(20)` | `Registered` / `Started` / `DNS` |
| `StartPlace` | `int` | |
| `LastModifiedUtc` | `datetimeoffset` | For conflict resolution + delta sync |
| `LastModifiedBy` | `nvarchar(50)` | `"android-gate1"`, `"desktop"` |

Index on `(CompetitionDate, LastModifiedUtc)` for efficient delta queries.

### ChangeLog

| Column | Type | Notes |
|---|---|---|
| `Id` | `bigint identity` | PK |
| `CompetitionDate` | `date` | |
| `StartNumber` | `int` | Which runner |
| `FieldName` | `nvarchar(50)` | Which field changed |
| `OldValue` | `nvarchar(200)` | Nullable (null on insert) |
| `NewValue` | `nvarchar(200)` | |
| `ChangedAtUtc` | `datetimeoffset` | |
| `ChangedBy` | `nvarchar(50)` | Device/source identifier |

This gives you the "long list, latest first" audit view you originally wanted.

---

## API Contracts

### Authentication

Same as current: `X-Api-Key` header on all mutating requests. GETs are open (or optionally keyed too — your call).

### 1. Upsert full runners (Desktop bulk upload)

```
PUT /api/competitions/{date}/runners
```

Desktop sends the full startlist from local DB. API upserts all, applies last-write-wins per field.

**Request:**
```json
{
  "source": "desktop",
  "runners": [
    {
      "startNumber": 101,
      "siChipNo": "8001234",
      "name": "Jānis",
      "surname": "Bērziņš",
      "className": "M21E",
      "clubName": "OK Māja",
      "country": "LV",
      "status": "Registered",
      "startPlace": 1,
      "lastModifiedUtc": "2026-03-28T08:15:00Z"
    }
  ]
}
```

**Response `200`:**
```json
{
  "inserted": 45,
  "updated": 3,
  "skippedAsOlder": 1
}
```

`skippedAsOlder` — rows where the incoming `lastModifiedUtc` was older than what's already in the DB. This is how last-write-wins works server-side.

---

### 2. Partial update (Android / any device)

```
PATCH /api/competitions/{date}/runners/{startNumber}
```

Only include fields that changed. The API only overwrites those fields. Last-write-wins applies per-row (not per-field — see rationale below).

**Request:**
```json
{
  "status": "Started",
  "lastModifiedUtc": "2026-03-28T09:32:15.123Z",
  "source": "android-gate1"
}
```

**Response `200`:**
```json
{
  "startNumber": 101,
  "applied": true
}
```

**Response `200` (conflict — older timestamp):**
```json
{
  "startNumber": 101,
  "applied": false,
  "reason": "serverTimestamp_is_newer",
  "serverLastModifiedUtc": "2026-03-28T09:32:16.000Z"
}
```

**Response `404`:** runner doesn't exist for this competition.

**Important design note on conflict granularity:** I'd recommend last-write-wins **per-row** rather than per-field, because:
- Per-field requires tracking timestamps on every column — complex schema
- In practice, two devices rarely edit different fields on the same runner simultaneously
- Status changes (Started/DNS) are the most common field-level update and those happen at a single physical gate

If you still want per-field, each field needs its own timestamp column, which bloats the schema significantly. Let me know your preference.

---

### 3. Get runners (full or delta)

```
GET /api/competitions/{date}/runners
GET /api/competitions/{date}/runners?changedSince=2026-03-28T09:00:00Z
```

**Response `200`:**
```json
{
  "competitionDate": "2026-03-28",
  "serverTimeUtc": "2026-03-28T09:45:00Z",
  "runners": [
    {
      "startNumber": 101,
      "siChipNo": "8001234",
      "name": "Jānis",
      "surname": "Bērziņš",
      "className": "M21E",
      "clubName": "OK Māja",
      "country": "LV",
      "status": "Started",
      "startPlace": 1,
      "lastModifiedUtc": "2026-03-28T09:32:15.123Z",
      "lastModifiedBy": "android-gate1"
    }
  ]
}
```

`serverTimeUtc` is critical — the client saves this and uses it as `changedSince` on the next request. This avoids clock-skew issues between devices and the server.

Response compression: use ASP.NET Core's `UseResponseCompression()` middleware — handles gzip/brotli based on `Accept-Encoding` automatically.

---

### 4. Get changelog (observability)

```
GET /api/competitions/{date}/changelog?max=100
GET /api/competitions/{date}/changelog?startNumber=101
```

**Response `200`:**
```json
{
  "entries": [
    {
      "id": 4523,
      "startNumber": 101,
      "fieldName": "Status",
      "oldValue": "Registered",
      "newValue": "Started",
      "changedAtUtc": "2026-03-28T09:32:15.123Z",
      "changedBy": "android-gate1"
    }
  ]
}
```

---

### 5. Competition management

```
POST /api/competitions          — create today's competition
GET  /api/competitions          — list all (most recent first)
GET  /api/competitions/{date}   — get one (with runner count, last sync time)
```

---

## Sync Protocol Summary

| Scenario | Flow |
|---|---|
| **Android makes a change** | Save locally → `PATCH /runners/{id}` → on success, mark synced locally |
| **Android pulls updates** | `GET /runners?changedSince={lastServerTime}` → merge into Room DB → save returned `serverTimeUtc` for next call |
| **Android full refresh** | Manual button → `GET /runners` (no `changedSince`) → replace all local data |
| **Desktop auto-push** | Poll local DB for changes → `PUT /runners` (bulk) → on success, record sync watermark |
| **Desktop manual push** | Same as auto-push but triggered by button |
| **Desktop shows field changes** | `GET /runners?changedSince=...` or `GET /changelog` |

---

## Open Questions

Before building, a couple of things to finalize:

1. **Per-row vs per-field last-write-wins** — I recommended per-row above. Are you ok with that, or do you specifically need two devices to be able to update different fields on the same runner and both "win"?

2. **Desktop local DB format** — What technology/format is it? (SQL Server, Access, SQLite, something else?) This determines how the WinForms app reads it and detects changes. Does it have any timestamp/version column we can use to detect which rows changed?

3. **Should GETs also require API key**, or are they public? Public makes debugging from a browser easier, but limits security.

4. **Do Android devices ever need to create runners** (e.g., late registration at the start), or only update existing ones uploaded by Desktop?

---

**User**

I want to have Runner "status" as separate table and save StatusId. I don't want to manually create competitions for each day, desktop app should auto-create those upon startlist upload.
auth - GET without auth, upsert/delete - with key in header. We should be able to manually change the key on devices.
1. it is OE12 that manages DBISAM database. I have driver to interact it, but I can't  watch the changes - I will need to grab the entire DB myself and upload it all. 
2. I will have max 10 devices. SignalR is NOT ok, because connection may be weak in wilderness. 
3. periodic refresh would be fine.  I want to keep this over API, not SignalR. But the connection is good, I can refresh every 30seconds or so.
4. there is just OE12 app that writes to dektop DB. There will be 2 connections directly to DB - the new app we are planning and OE12.

---

**Cursor**

Clear. Let me consolidate everything into the final architecture with all decisions locked in.

---

## Final Architecture

```
┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│  Android #1  │  │  Android #2  │  │  Android #N  │
│  (up to 10)  │  │              │  │              │
└──────┬───────┘  └──────┬───────┘  └──────┬───────┘
       │ PATCH (changed   │                  │
       │   fields only)   │                  │
       │ GET every ~30s   │                  │
       ▼                  ▼                  ▼
┌─────────────────────────────────────────────────────┐
│             .NET 8 API  (App Service)               │
│                                                     │
│  Auth: GET = open, POST/PUT/PATCH/DELETE = X-Api-Key│
│  Response compression: gzip/brotli                  │
│  Conflict resolution: last-write-wins (per-row)     │
│                                                     │
│  Azure SQL Database (serverless)                    │
│  ┌──────────┐ ┌──────────┐ ┌───────┐ ┌───────────┐│
│  │ Runners  │ │ChangeLog │ │Status │ │Competition││
│  │          │ │          │ │(lookup│ │(auto-      ││
│  │          │ │          │ │ table)│ │ created)   ││
│  └──────────┘ └──────────┘ └───────┘ └───────────┘│
└──────────────────────────┬──────────────────────────┘
                           │
                           ▼
                ┌──────────────────┐
                │  Desktop App     │
                │  (WinForms)      │
                │                  │
                │  • Reads entire  │
                │    DBISAM DB     │
                │  • Bulk uploads  │
                │    to API        │
                │  • Auto-push at  │
                │    intervals     │
                │  • Manual push   │
                │    button        │
                └────────┬─────────┘
                         │ reads (via driver)
                         ▼
                ┌──────────────────┐
                │  DBISAM DB       │
                │  (managed by     │
                │   OE12)          │
                └──────────────────┘
```

---

## Data Model (Azure SQL)

### Statuses (seed data)

| Id | Name |
|---|---|
| 1 | `Registered` |
| 2 | `Started` |
| 3 | `DNS` |

Seeded on migration. If you need more later (e.g. `Disqualified`), add a row.

### Competitions

| Column | Type | Notes |
|---|---|---|
| `Date` | `date` | PK |
| `Name` | `nvarchar(200)` | Nullable, optional display name |
| `CreatedAtUtc` | `datetimeoffset` | |

Auto-created by the bulk upload endpoint when a new date is encountered.

### Runners

| Column | Type | Notes |
|---|---|---|
| `CompetitionDate` | `date` | PK (composite), FK → Competitions |
| `StartNumber` | `int` | PK (composite) |
| `SiChipNo` | `nvarchar(20)` | |
| `Name` | `nvarchar(100)` | |
| `Surname` | `nvarchar(100)` | |
| `ClassName` | `nvarchar(50)` | |
| `ClubName` | `nvarchar(100)` | |
| `Country` | `nvarchar(10)` | |
| `StatusId` | `int` | FK → Statuses, default `1` (Registered) |
| `StartPlace` | `int` | |
| `LastModifiedUtc` | `datetimeoffset` | For conflict resolution + delta sync |
| `LastModifiedBy` | `nvarchar(50)` | Device identifier |

Index: `IX_Runners_CompetitionDate_LastModifiedUtc` on `(CompetitionDate, LastModifiedUtc)`.

### ChangeLog

| Column | Type | Notes |
|---|---|---|
| `Id` | `bigint identity` | PK |
| `CompetitionDate` | `date` | |
| `StartNumber` | `int` | |
| `FieldName` | `nvarchar(50)` | |
| `OldValue` | `nvarchar(200)` | Null on insert |
| `NewValue` | `nvarchar(200)` | |
| `ChangedAtUtc` | `datetimeoffset` | |
| `ChangedBy` | `nvarchar(50)` | |

No FK constraints on ChangeLog — it's append-only audit data, never joined in hot paths.

---

## API Contracts (Final)

Base URL: `https://{your-app}.azurewebsites.net/api`

Auth: `X-Api-Key` header required on all non-GET requests. Key stored in app config, settable per-device in the Android/Desktop app settings.

### 1. Bulk upload runners (Desktop)

```
PUT /api/competitions/{date}/runners
X-Api-Key: {key}
```

Auto-creates the competition if it doesn't exist for `{date}`. Accepts the **full** startlist from DBISAM. The API diffs against current state:
- New `startNumber` → insert, log to ChangeLog
- Existing runner with changed fields → update only changed fields, log each to ChangeLog
- Unchanged runners → skip (no changelog noise)

Conflict resolution: incoming `lastModifiedUtc` must be ≥ server's `LastModifiedUtc` for that row, otherwise skip.

**Request:**
```json
{
  "source": "desktop",
  "lastModifiedUtc": "2026-03-28T08:15:00Z",
  "runners": [
    {
      "startNumber": 101,
      "siChipNo": "8001234",
      "name": "Jānis",
      "surname": "Bērziņš",
      "className": "M21E",
      "clubName": "OK Māja",
      "country": "LV",
      "statusId": 1,
      "startPlace": 1
    }
  ]
}
```

Note: `lastModifiedUtc` is at the top level (one timestamp for the whole batch), since DBISAM has no per-row timestamps — the timestamp is "when Desktop read the DB." Each runner in the array has no individual timestamp.

**Response `200`:**
```json
{
  "competitionDate": "2026-03-28",
  "competitionCreated": false,
  "inserted": 45,
  "updated": 3,
  "unchanged": 150,
  "skippedAsOlder": 1
}
```

---

### 2. Partial update runner (Android)

```
PATCH /api/competitions/{date}/runners/{startNumber}
X-Api-Key: {key}
```

Only include fields that changed. Omitted fields are not touched.

**Request (runner checked in at gate):**
```json
{
  "statusId": 2,
  "lastModifiedUtc": "2026-03-28T09:32:15.123Z",
  "source": "android-gate1"
}
```

**Request (edit SI chip):**
```json
{
  "siChipNo": "8009999",
  "lastModifiedUtc": "2026-03-28T09:33:00.000Z",
  "source": "android-ref3"
}
```

**Response `200` (applied):**
```json
{
  "startNumber": 101,
  "applied": true
}
```

**Response `200` (rejected — server row is newer):**
```json
{
  "startNumber": 101,
  "applied": false,
  "reason": "server_timestamp_is_newer",
  "serverLastModifiedUtc": "2026-03-28T09:32:16.000Z"
}
```

**Response `404`:** runner doesn't exist.

---

### 3. Get runners (full or delta)

```
GET /api/competitions/{date}/runners
GET /api/competitions/{date}/runners?changedSince=2026-03-28T09:00:00.000Z
```

Without `changedSince`: returns all runners (manual full refresh).
With `changedSince`: returns only runners where `LastModifiedUtc > changedSince` (delta sync).

**Response `200`:**
```json
{
  "competitionDate": "2026-03-28",
  "serverTimeUtc": "2026-03-28T09:45:00.123Z",
  "count": 2,
  "runners": [
    {
      "startNumber": 101,
      "siChipNo": "8001234",
      "name": "Jānis",
      "surname": "Bērziņš",
      "className": "M21E",
      "clubName": "OK Māja",
      "country": "LV",
      "statusId": 2,
      "statusName": "Started",
      "startPlace": 1,
      "lastModifiedUtc": "2026-03-28T09:32:15.123Z",
      "lastModifiedBy": "android-gate1"
    }
  ]
}
```

`serverTimeUtc` — client saves this and uses as `changedSince` on next poll. This avoids clock-skew issues between device clocks and the server.

---

### 4. Get changelog

```
GET /api/competitions/{date}/changelog?max=100
GET /api/competitions/{date}/changelog?startNumber=101
```

**Response `200`:**
```json
{
  "entries": [
    {
      "id": 4523,
      "startNumber": 101,
      "fieldName": "StatusId",
      "oldValue": "1",
      "newValue": "2",
      "changedAtUtc": "2026-03-28T09:32:15.123Z",
      "changedBy": "android-gate1"
    }
  ]
}
```

---

### 5. Get statuses (lookup)

```
GET /api/statuses
```

**Response `200`:**
```json
[
  { "id": 1, "name": "Registered" },
  { "id": 2, "name": "Started" },
  { "id": 3, "name": "DNS" }
]
```

Cached on app startup by all clients.

---

### 6. List competitions

```
GET /api/competitions
```

**Response `200`:**
```json
[
  {
    "date": "2026-03-28",
    "name": null,
    "runnerCount": 198,
    "createdAtUtc": "2026-03-28T07:00:00Z"
  }
]
```

---

### 7. Delete runners (optional, for cleanup)

```
DELETE /api/competitions/{date}/runners
X-Api-Key: {key}
```

Clears all runners for a competition date. Useful if Desktop needs to do a clean re-upload.

---

## Sync Flows

### Android (every ~30 seconds)

```
1. Poll:  GET /competitions/{today}/runners?changedSince={lastServerTimeUtc}
2. Merge: upsert returned runners into local Room DB
3. Save:  store returned serverTimeUtc for next poll
```

### Android (user action)

```
1. User taps "Started" → update Room DB → queue PATCH
2. Attempt PATCH /runners/{startNumber} immediately
3. If online → success → mark synced
4. If offline → PendingSyncWorker retries later
```

### Desktop (auto-push, every N seconds, configurable)

```
1. Read entire startlist from DBISAM via driver
2. PUT /competitions/{today}/runners (full list)
3. API diffs, updates only changed rows, logs to ChangeLog
4. Display response summary in UI (inserted/updated/skipped)
```

### Desktop (manual push)

Same as auto-push but triggered by button click.

---

## Project Structure

```
/AndroidReferee
  /api/StartRef.Api/          ← .NET 8 API (refactor existing)
  /app/                       ← Android app (refactor existing)

/Desktop
  /StartRef.Desktop/          ← WinForms app (new)
    - MainForm.cs
    - DbIsamReader.cs         ← reads OE12 database
    - ApiClient.cs            ← HTTP calls to API
    - SyncService.cs          ← timer-based auto-push logic
    - Settings/               ← API URL, key, push interval
```

---

## Remaining question

One thing I want to confirm before this is final:

**Does OE12's DBISAM have a row-level timestamp or version column?** If yes, the Desktop can use it as `lastModifiedUtc` per-runner in the bulk upload, giving much better conflict resolution. If no, the entire batch gets a single "snapshot timestamp" (the moment the desktop read the DB), which means a desktop upload will always overwrite any field-level changes from Android that happened after the last desktop push. 

That tradeoff is: if a referee marks runner 101 as "Started" at 09:32, and then Desktop pushes at 09:33 with `statusId: 1` (Registered, because OE12 doesn't know about the gate check-in), the desktop's newer timestamp would overwrite the referee's change. We'd need to handle Status specially if that's the case — e.g. Desktop never overwrites Status, only Android can set it.

---

**User**

yes, desktop should never overwrite the status. Also >> when updating from API to local >> I am only interested to mark "did not start" users. I do NOT want to mark started ones.

No, there is no timestamp/version in DBISAM :(

Ignore what is in StartRef.API - I have moved it to API folder. 

lets no make separate "AndoirdGate" program, lets do it all in AndroidReferee, there will be another view that is meant for users to read SI cards before start. Idea is, that there is this screen: 
- time on top
- uncoloured list of people in that minute
- Read and OK>> bright green bacgroun to time field on top.
- after read >> just green
- If read SI that is not found or 5minutes - show green light (background to time field)
if the read is "red", then there are two options >> press on runner in the minute to assign chip. OR >> click "handle manually".


---

**Cursor**

**Clarifying the Gate / SI Reader screen**

> **How does the Android device read SI chips? Is it via USB OTG to a SportIdent station, Bluetooth to a BSM station, or NFC built into the phone?**
> - USB OTG to SI station (e.g. BSM-7/8) ✔
> - Bluetooth to SI station
> - NFC built into the Android phone
> - Manual entry (type chip number)
> - Other / not decided yet

> **Is this Gate/SI-reader screen part of the scope for the plan we're building now, or do you want to focus on the API + Desktop + sync first and add the Gate screen later?**
> - Include Gate screen in this plan ✔
> - Focus on API + Desktop + sync first, Gate screen later

I have a thorough understanding of the full codebase now. Let me ask one clarification before finalizing the plan.

**Gate screen color logic clarification**

> **For the Gate/SI-reader screen, when an SI chip is read, which color logic is correct?**
> - GREEN if chip found within current minute. Also GREEN if found within +/-5 min. RED if chip not found at all OR start time >5 min away.
> - GREEN if chip found anywhere in startlist (regardless of time). RED only if chip not found at all.
> - Something else (I will describe)
>
> Answer: orange if not now, but found +-5min, show the name that it is found, menu to approve/handle manuall.

> **On the Gate screen, when a runner's chip is read and matched, should the app automatically mark them as 'Started' (statusId=2)?**
> - Yes, auto-mark as Started on successful chip read ✔
> - No, just visual confirmation. Status is changed separately on the Referee startlist screen.

Good, that clarifies the Gate screen behavior. Let me now create the full plan.

---

**User**

I only want DNS saved in DBISAM, the Android devices should overwrite "Registered" to "started". Keep in mind, that in the normal startlist view on Android, there are 3 statuses - normal/default/white = Registered, with checkbox ticked/light green = Started, with checkbox unticked and red Background = DNS.

The Desktop app seems to be missing the the "pull", the entire idea that it updates runners data from API to local.  That is should regularly pull. Pull before push. So it is sync, not just auto-push

---

**Cursor**

Good catches, let me make sure I understand both corrections precisely.

**Status sync rules (corrected):**

| Direction | Rule |
|---|---|
| Android → API | Can set any status: Registered → Started (gate read), Registered → DNS (referee action) |
| API → Android (pull) | Only apply DNS from server. Never overwrite local Registered/Started with server's Started. This prevents a remote gate's "Started" from appearing on your device. |
| Desktop → API (push) | Upload full list from DBISAM. API preserves "Started" — if server has Started but DBISAM says Registered, keep Started. Accept DNS from DBISAM. |
| API → Desktop (pull) | Pull DNS statuses from API and **write them back to DBISAM** so OE12 sees them for results. |

**Desktop sync cycle (corrected — full bidirectional):**

```
1. PULL from API   → get latest runners (especially DNS from field devices)
2. WRITE to DBISAM → save DNS statuses so OE12 can see them
3. READ from DBISAM → get full startlist (includes OE12 edits + DNS we just wrote)
4. PUSH to API     → bulk upload, API diffs and preserves Started status
```

This means the Desktop app needs **write access** to DBISAM, not just read. And the only thing it writes back is DNS status.

**Desktop DBISAM write access**

> **Can your DBISAM driver WRITE to the database (specifically to update a runner's status to DNS)? Or is OE12 the only process that writes?**
> - Yes, my driver can write to DBISAM too — both OE12 and my app can write ✔
> - No, my driver is read-only. Only OE12 writes to DBISAM.

> **Where can DNS originate from?**
> - Both: OE12 can mark DNS in DBISAM, AND Android referees can mark DNS in the field ✔
> - Only Android referees mark DNS in the field (OE12 never sets DNS)
> - Only OE12 marks DNS (Android never sets DNS)

---

**User**

api >> android . can can set everything except started/dns back to registered.

---

**Cursor**

Got it. Let me consolidate the full API → Android status transition rules:

**API → Android pull — status rules:**

| Server status | Local status | Action |
|---|---|---|
| Registered (1) | Started (2) | **Keep local** — cannot downgrade |
| Registered (1) | DNS (3) | **Keep local** — cannot downgrade |
| Started (2) | Registered (1) | **Keep local** — don't apply Started from remote |
| DNS (3) | Registered (1) | **Apply DNS** |
| DNS (3) | Started (2) | **Apply DNS** |
| Registered (1) | Registered (1) | No change |
| Started (2) | Started (2) | No change |
| DNS (3) | DNS (3) | No change |

In short: the only status change accepted from API pull is **→ DNS**. All other status transitions are ignored. All non-status fields (name, surname, siChipNo, className, clubName, country, startPlace) are always applied freely.

Let me update the plan to reflect all the corrections from this conversation.

Now let me update the plan with both corrections: the precise status sync rules and the Desktop bidirectional sync.

Now update the Sync Service section in the plan body:

Now update the Desktop section to reflect bidirectional sync:

Now update the Desktop UI mockup to reflect sync (not just push):

Now update the PUT endpoint description for Desktop bulk upload — it needs to handle DNS from DBISAM properly:

Now update the sync flow diagram and the key decisions table:

Plan updated with both corrections:

1. **Status sync rules refined across all directions:**
   - **Android → API**: can set Started and DNS freely
   - **API → Android**: all non-status fields applied freely; status only allows transitions **to DNS** — never applies Started remotely, never downgrades to Registered
   - **Desktop → API**: can escalate to DNS (from OE12) but never downgrades Started or DNS back to Registered
   - **DNS can originate from both** OE12 (via DBISAM) and Android referees

2. **Desktop is now bidirectional sync**, not just push. Each cycle:
   1. **PULL** from API — get latest state (DNS from field devices)
   2. **WRITE** DNS statuses to DBISAM — so OE12 sees them
   3. **READ** from DBISAM — get full startlist with OE12 edits
   4. **PUSH** to API — bulk upload with diff

The plan is ready for your review. Any other corrections, or shall we start building?

---

**User**

do apply "started" from remote. 

The pull from remote should also update all other fields >> Name, Surname, Club, SI card, status. Do NOT update class. none of the apps should be able to update class.

---

**Cursor**

Now update the PATCH endpoint to exclude className, and add a global rule about className immutability:

Now update the key decisions table:

Also need to update the Desktop bidirectional sync description — it should now pull Started statuses too, not just DNS:

Updated. Here's the summary of changes:

**Status sync — now forward-only everywhere:**
- **API → Android**: apply both Started AND DNS from server. Never downgrade to Registered.
- Status can only move forward: Registered → Started → DNS (and Registered → DNS directly).

**className is immutable:**
- Set only during initial bulk upload INSERT.
- API PATCH silently ignores `className` if present.
- Android never sends it in PATCH.
- Desktop bulk upload: className is set on INSERT but not changed on UPDATE.
- Android pull: className is NOT applied from server.

**Desktop DBISAM write**: still only writes DNS back (OE12 doesn't need "Started" info).

Anything else to adjust, or ready to start building?

---

**User**

on the desktop app, 
-) in the log - show all users updated (including names). And write that log in txt.file next to app. And also show in log, which device initiated changes. 
-) Not sure that "sync now" and "full reload means". I do want an option to push and overwrite all data, except statuses (i.e. mark all runners as modified just now).

---

**Cursor**

Now update the todo for desktop-ui to reflect the richer logging:

We also need a corresponding API-side mechanism for "Force Push All". Let me add that to the bulk upload endpoint:

Updated. To summarize the changes:

**Desktop log** is now detailed:
- Shows each affected runner by start number + full name
- Shows which field changed with old → new values
- PULL entries include which device made the change (e.g. "android-gate1")
- Log is both displayed in the UI and appended to `sync_log.txt` next to the executable

**Buttons clarified:**
- **Sync Now** — normal sync cycle (PULL → WRITE DNS → READ → PUSH), just manually triggered
- **Force Push All** — reads DBISAM and uploads with `lastModifiedUtc = now`, forcing all non-status fields to overwrite the server. Statuses still protected.

Anything else to adjust?

