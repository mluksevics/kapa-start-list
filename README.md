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
      │ writes
      ▼
  DBISAM DB ◄── ChipNr/KatNr/StartTime written back ──────────┐
      │                                                         │
      │ scanned (Force Push)                                    │
      ▼                                                         │
 Desktop App ──── PUT /runners (bulk) ──► .NET API ◄──── PATCH /runners ──── Android (referee)
                                          (Azure SQL)          ▲
                                              │                │
                                  GET /runners?changedSince ───┘
                                          Android / Desktop (~30–60s poll)
```

**Sync flow (Desktop, regular cycle, every ~60 s):**
1. PULL from API (`GET /runners?changedSince={watermark}`) — receive changes from field devices
2. Write ChipNr, KatNr, and StartTime changes back to DBISAM via DbBridge DLL
3. Save server timestamp as watermark for the next delta

**Force Push All (Desktop, on demand):**
1. Regular sync cycle first
2. Scan all runners from DBISAM (start numbers 1–4000)
3. Bulk-upload to API (`PUT /runners`) — overwrites non-status fields; status never downgrades

**Sync flow (Android, every ~30 s):**
- Delta poll: `GET /runners?changedSince={watermark}`
- Forward-only status merge: Started and DNS are applied, never downgraded

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
