# StartRef — Orienteering Start-List Management System

Real-time start-list management for orienteering events. Three components talk to a central .NET API backed by Azure SQL, keeping data in sync across an event PC (OE12), multiple Android referee/gate devices, and a cloud database — even with limited connectivity in the field.

---

## Components

| Component | Path | Description |
|-----------|------|-------------|
| [API](API/README.md) | `API/` | .NET 8 minimal API, Azure SQL, source of truth |
| [Android](AndroidReferee/README.md) | `AndroidReferee/` | Kotlin/Compose app for referees and gate operators |
| [Desktop](Desktop/StartRef.Desktop/README.md) | `Desktop/StartRef.Desktop/` | WinForms app that syncs OE12/DBISAM ↔ API |

---

## Architecture

```
OE12 (event software)
  -> writes local event data
  -> DBISAM DB

Desktop App
  -> reads/scans DBISAM (Force Push source)
  -> writes ChipNr/KatNr/StartTime back to DBISAM (via DbBridge.dll)
  -> PUT /runners (bulk) to .NET API
  -> GET /runners?changedSince from .NET API (delta pull)

Android (referee/gate)
  -> PATCH /runners to .NET API
  -> GET /runners?changedSince from .NET API (delta pull)

.NET API (Azure SQL)
  <-> central source of truth for mobile + desktop sync
```

**Sync flow (Desktop, regular cycle, every ~60 s):**
1. Pull deltas from API (`GET /runners?changedSince={watermark}`) to receive field updates (each runner may include changelog-backed `changedFields` so only relevant columns are considered for DBISAM writes)
2. Compare pulled values vs current DBISAM values
3. Write only changed ChipNr/KatNr/StartTime back to DBISAM via DbBridge DLL
4. Save server timestamp as watermark for next delta pull

**Force Push All (Desktop, on demand):**
1. Run regular sync cycle first
2. Scan all runners from DBISAM (start numbers 1-4000)
3. Bulk-upload to API (`PUT /runners`)
4. API updates non-status fields; status never downgrades

**Sync flow (Android, every ~30 s):**
- Delta poll: `GET /runners?changedSince={watermark}` (optional per-runner `changedFields` drives brief UI highlights on the start list)
- Status from API is applied as returned (including **Registered** after clearing Started/DNS on another device). **Bulk PUT** from Desktop still uses forward-only status rules so OE12 pushes do not wipe gate state.

**Android gate:**
- USB OTG SportIdent BSM-7/8 station reads SI chips
- Card matched → runner auto-marked Started + PATCH queued
- Wrong minute → orange signal + Approve / Handle Manually
- Not found → red signal + tap runner to assign chip

---

## Status model

| Id | Name | Set by |
|----|------|--------|
| 1 | Registered | Default |
| 2 | Started | Android gate (SI chip read) |
| 3 | DNS | Android referee |

**Rule:** status can only escalate. Started and DNS are never downgraded back to Registered by any component.

---

## Lookup tables

Classes and clubs are stored in separate `Classes` and `Clubs` tables. `Runner` holds only `ClassId`/`ClubId`; names are resolved at read time. The Desktop **Push Clubs** button populates the `Clubs` table from DBISAM. Class names are upserted during bulk upload.

---

## Quick start

### API

```bash
cd API
# Configure connection string in appsettings.Development.json
dotnet run
# DB is created and migrated automatically on first run
```

### Desktop

```bash
cd Desktop/StartRef.Desktop
dotnet run
# Enter API URL, API key, and DBISAM path in Settings
# Place DbBridge.dll alongside the executable
```

### Android

1. Open `AndroidReferee/` in Android Studio
2. Sync Gradle
3. Run on device — configure API URL and key in Settings

---

## Solution

Open `StartRef.sln` in Visual Studio to work on the API and Desktop projects together.

```
StartRef.sln
├── API/StartRef.Api.csproj
└── Desktop/StartRef.Desktop/StartRef.Desktop.csproj
```

The Android project is a separate Gradle project in `AndroidReferee/`.
