# StartRef — Orienteering Start-List Management System

Real-time start-list management for orienteering events. Three components talk to a central .NET API backed by Azure SQL, keeping data in sync across an event PC (OE12), multiple Android referee/gate devices, and a cloud database — even with limited connectivity in the field.

---

## Components

| Component | Path | Description |
|-----------|------|-------------|
| [API](API/README.md) | `API/` | .NET 8 minimal API, Azure SQL, source of truth |
| [Android](AndroidReferee/README.md) | `AndroidReferee/` | Kotlin/Compose app for referees and gate operators |
| [Desktop](Desktop/README.md) | `Desktop/` | WinForms app that syncs OE12/DBISAM ↔ API |

---

## Architecture

```
OE12 (event software)
  -> OE operator creates/edits runners, assigns classes/clubs/start times
  -> writes into local DBISAM database

Desktop App (Windows, event PC)
  -> reads DBISAM via DbBridge.dll (closed-source Delphi DLL)
  -> pulls delta updates from API → writes ChipNr / KatNr / StartTime back to DBISAM
  -> bulk-uploads runners to API on demand (multiple push modes)
  -> pushes Clubs and Classes lookup tables to API

Android (referee/gate devices, multiple)
  -> marks runners as Started (gate) or DNS (referee)
  -> edits runner details (SI chip, class, club, start time)
  -> PATCHes every change directly to API; failed PATCHes are queued and retried
  -> pulls delta from API every ~30 s to stay current

.NET API (Azure SQL)
  <- single source of truth for all devices
  -> runner data: GET /runners?changedSince (delta) or without for full list
  -> bulk upsert: PUT /runners (from Desktop)
  -> single-runner patch: PATCH /runners/{startNumber} (from Android)
  -> lookups: GET/PUT /api/lookups/classes|clubs
```

---

## Data model highlights

| Entity | Key fields |
|--------|-----------|
| `Runner` | `StartNumber` (PK), `Name`, `Surname`, `SiChipNo`, `ClassId`, `ClubId`, `StartTime`, `StatusId`, `LastModifiedUtc`, `LastModifiedBy` |
| `Class` | `Id` (PK), `Name`, **`StartPlace`** (which physical start location this class uses: 0 = any/unset, 1–3 = specific lanes) |
| `Club` | `Id` (PK), `Name` |

`Country` and runner-level `StartPlace` were **removed** in the data-model refactor. Start-place filtering is now done via the class lookup (`class.StartPlace`).

---

## Sync flows

### Desktop → API (push)

Several push modes exist, each suited to different situations:

| Mode | When to use | API `touchAll` | Phantom runners filtered |
|------|-------------|:-:|:-:|
| **Force Push All** | After bulk OE12 data entry (full overwrite) | ✓ `true` | ✓ |
| **Push selected** | After correcting a specific bib range in OE12 | ✓ `true` | ✓ |
| **Push all changes** | Routine push; API detects what actually changed | — `false` | ✓ |
| **Upload new** | Add runners OE12 added since last push | — `false` | ✓ |

**`touchAll = true`** tells the API to bump `LastModifiedUtc` on every uploaded runner even if field values are unchanged. This guarantees Android's next incremental poll receives the full updated set — essential after a bulk OE12 import.

**`touchAll = false`** (default) means the API only bumps `LastModifiedUtc` for rows where at least one non-status field actually changed. Android only polls the genuine deltas.

All push modes skip bibs where `idNr == 0` (empty start-number slot in DBISAM) so ghost runners are never created in the API.

### API → Desktop (pull / regular cycle)

```
1. GET /api/competitions/{date}/runners?changedSince={watermark}
   Receives delta: runners whose LastModifiedUtc > watermark.
   Each runner may include changedFields list (API changelog-backed)
   so only relevant columns are written to DBISAM.

2. For each changed runner (skipping changes made by this desktop device):
     SiChipNo  → DbChangeChipNrByStartNr
     ClassId   → DbChangeKatNrByStartNr
     StartTime → DbChangeStartTimeByStartNr
     DNS       → DbChangeDnsByStartNr  (stub — pending DLL support)
   Name, Surname, Club: logged but not written (DLL APIs not yet available).

3. Save response serverTimeUtc as watermark for next pull.
```

Run automatically every N seconds (**Auto-pull**) or manually via **Pull Now**.

### API → Android (delta poll)

Every ~30 s: `GET /runners?changedSince={watermark}` → merge all fields into Room DB, trust API status fully (status can go up or down — e.g. a DNS can revert to Registered if corrected on another device).

### Android → API (PATCH)

Every user action (start, DNS, edit) is immediately PATCHed to the API. Failed PATCHes are queued locally in `pending_sync` and retried by WorkManager. **Flush pending updates** in Android settings replays the queue immediately.

### Android Force Pull

**Force Pull all** in Android settings: fetches the full runner list from the API (`changedSince = null`), deletes all local runners, then inserts the fresh API response. Resets watermark. Use to recover from a corrupted local state.

---

## Status model

| Id | Name | Set by |
|----|------|--------|
| 1 | Registered | Default |
| 2 | Started | Android gate (SI chip read or referee tap) |
| 3 | DNS | Android referee |

**Status rules by operation:**

- **Desktop bulk PUT (`ResolveStatusForBulkUpload`):** Desktop always sends `StatusId = 1`. The API preserves existing `Started (2)` or `DNS (3)` — OE12 pushes never wipe gate state.
- **Android PATCH:** Applied directly; server stores the value as received. Any referee can toggle Started or DNS off (back to Registered).
- **Android / Desktop GET merge:** The received `statusId` is applied as-is (including downgrades) so all devices converge on the latest state set by a referee or gate.

---

## Lookup tables

`Classes` and `Clubs` are stored in dedicated tables and referenced from `Runner` by `ClassId`/`ClubId`. Class names are resolved at read time; the runner record does not store them.

**`Class.StartPlace`** (0 = any/unset, 1–3 = specific start lane) is the authoritative filter used by both the Android start list and the gate screen when a "Start Place" setting is selected. The Desktop **Push Classes** button upserts class names (and `StartPlace = 0` for now) to the API; the Desktop **Push Clubs** button upserts club names.

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
cd Desktop
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
└── Desktop/StartRef.Desktop.csproj
```

The Android project is a separate Gradle project in `AndroidReferee/`.
