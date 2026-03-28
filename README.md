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
  DBISAM DB ◄──── DNS written back ────────────────────────┐
      │                                                      │
      │ read                                                 │
      ▼                                                      │
 Desktop App ──── PUT /runners (bulk) ──► .NET API ◄──── PATCH /runners ──── Android (referee)
                                          (Azure SQL)         ▲
                                              │               │
                                  GET /runners?changedSince ──┘
                                          Android (gate, ~30s poll)
```

**Sync flow (Desktop, every ~60 s):**
1. PULL from API — get latest statuses from field devices
2. Write DNS statuses to DBISAM (so OE12 sees them)
3. Read full start list from DBISAM
4. Push to API (bulk upsert)

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
| 3 | DNS | Android referee, or Desktop (from OE12) |

**Rule:** status can only escalate. Started and DNS are never downgraded back to Registered by any component.

**`className` is immutable** after a runner's first upload. No app can change it via PATCH or subsequent bulk uploads.

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
