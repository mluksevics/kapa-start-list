# StartRef Desktop

.NET 8 WinForms application that runs on the **event PC** alongside OE12. It performs bidirectional synchronisation between the OE12 DBISAM database and the StartRef API, so that changes made by field Android devices (SI chip assignments, category corrections, start times) are written back to DBISAM, and new or corrected runner data from OE12 can be pushed up to the API.

---

## Pull cycle (API → Desktop → DBISAM)

Each regular pull cycle (auto-pull or **Pull Now**) runs these steps:

```
1. PULL       GET /api/competitions/{date}/runners?changedSince={watermark}
              → receives delta: runners whose LastModifiedUtc > watermark.
              Each runner optionally includes a changedFields list (changelog-backed)
              so only the columns that actually changed are considered for DBISAM writes.

2. WRITE-BACK For each changed runner where lastModifiedBy ≠ this device:
                SiChipNo  → DbChangeChipNrByStartNr
                ClassId   → DbChangeKatNrByStartNr
                StartTime → DbChangeStartTimeByStartNr
                StatusId 3 (DNS) → DbChangeDnsByStartNr  ⚠ stub — DLL not yet implemented
              Name / Surname / Club: logged with "NEEDS change" but not written (DLL APIs absent).
              DBISAM value is compared first; write skipped if already equal.

3. WATERMARK  Save response serverTimeUtc as watermark for next pull.
```

Implementation split:

- `Services/SyncService.cs` — orchestrates API pull/push, timer, watermark
- `Services/DbIsamRepository.cs` — DBISAM scan (push) and write-back (pull)
- `DbBridge/DbBridgeService.cs` — wraps `DbBridgeNative` P/Invoke calls

---

## Push modes (Desktop → API)

Runners with `idNr == 0` in DBISAM (empty bib slots) are always skipped by all push modes.

### Force Push All *(Advanced)*

```
1. Pull cycle (regular sync, gets latest field data and watermark).
2. Scan DBISAM start numbers 1–4000 via GetIdNrListByStartNr + GetTeilnInfoByIdNr.
3. PUT /api/.../runners?touchAll=true
   API skips field-level comparison; bumps LastModifiedUtc on every runner regardless
   of whether values changed. Android's next incremental poll sees every runner as updated.
   Status is preserved: incoming StatusId 1 never downgrades existing Started/DNS.
```

Use after a bulk OE12 data import to guarantee Android devices refresh their full list.

### Push selected *(Advanced)*

Same as Force Push All with `touchAll=true`, but the DBISAM scan covers only an inclusive start-number range. Opens a *From / To* dialog.

### Push all changes

```
1. Scan DBISAM 1–4000; filter out bibs with no participant data
   (no name, class, club, or chip — i.e. empty bib slots).
2. PUT /api/.../runners  (no touchAll; default touchAll=false)
   API field-compares each runner; only rows where at least one non-status field
   differs get their LastModifiedUtc bumped. Android only polls real deltas.
   Status is preserved the same way as Force Push.
```

Use for routine end-of-day sync when you don't need Android to re-receive everything.

### Upload new *(Advanced)*

```
1. GET full runner list from API (no changedSince) to load current API state.
2. Scan DBISAM 1–4000; filter meaningful runners (HasIsamParticipantData).
3. For each DBISAM runner: compare to API row. Include if new or any field differs.
4. PUT only the differing / new rows (touchAll=false). StatusId preserved from API.
```

Use after OE12 adds new runners that need to appear on Android devices.

### Push Clubs / Push Classes

Read the full club or class list from DBISAM and upsert to `/api/lookups/clubs` or `/api/lookups/classes`. Classes are sent with `StartPlace = 0` (the Desktop does not read start-place from DBISAM; set it in OE12 or directly via the API if needed).

---

## Buttons

| Button | Behaviour |
|--------|-----------|
| **Pull Now** | Runs one regular pull cycle immediately |
| **Push all changes** | Scan DBISAM → PUT without `touchAll`; API updates only rows that actually differ |
| **Push Clubs** | Upserts clubs to `/api/lookups/clubs` |
| **Push Classes** | Upserts classes to `/api/lookups/classes` |
| **Pull Past** | Opens a dialog to pull changes from the last N minutes (10 / 15 / 30 / 60 / 240 or custom). Useful after a connectivity gap. |
| **Peek API data** | Reads current API SQL row counts (competitors / clubs / classes) and logs them. Also runs automatically on startup. |
| **Advanced** (▶ collapsed) | Reveals **Force Push All**, **Push selected**, **Delete Today**, **Upload new**, **E** |
| **Cancel** | Left of the status line — cancels the running operation |
| **Force Push All** | Pull → scan 1–4000 → PUT `touchAll=true`. Every runner gets `LastModifiedUtc` bumped. |
| **Push selected** | Pull → scan a bib range → PUT `touchAll=true`. Dialog prompts *From / To*. |
| **Delete Today** | Deletes all competition data for the selected API date (double-confirmed). |
| **Upload new** | GET full API list → scan DBISAM → PUT only new/changed rows. |
| **E** | Opens the DbBridge Explorer for ad-hoc DLL diagnostics. |

---

## Log

Every change is logged with timestamp, start number, full name, field changed, and `lastModifiedBy` (which device made the change). The log is shown in the UI and simultaneously appended to `sync_log.txt` in the application directory.

Errors and warnings trigger a descending-tone beep (if failure sound is enabled, toggled with the 🔊/🔇 button).

---

## Settings

| Setting | Description |
|---------|-------------|
| API Base URL | StartRef API base URL |
| API Key | `X-Api-Key` header value sent with every request |
| DB Path | Path to the DBISAM database folder (browse button) |
| Device Name | Identifier sent as `lastModifiedBy` on every upload (default: `desktop`) |
| Day / Stage | Which OE12 stage/day to sync (loaded from DBISAM Etap table, auto-selects today) |
| Auto-pull | Enable/disable automatic pull on the interval |
| Interval | Sync interval in seconds (default: 60) |
| DB Code Page | ANSI code page for DbBridge string encoding (default: 1257 for Baltic languages) |
| Failure Sound | Toggle the error beep on/off |

Settings are persisted in `settings.json` next to the executable.

---

## DBISAM access — DbBridge DLL

DBISAM is accessed via **DbBridge.dll**, a native Delphi DLL that wraps the DBISAM engine. The app P/Invokes into it via `DbBridge/DbBridgeNative.cs`. Key functions used:

| DLL function | Purpose | Status |
|---|---|---|
| `DbOpen` / `DbClose` | Open/close the DBISAM folder | ✓ |
| `DbGetEtapInfo` | Read stage name and date for the day selector | ✓ |
| `DbGetIdNrListByStartNr` | Look up internal IdNr(s) for a start number | ✓ |
| `DbGetTeilnInfoByIdNr` | Read full participant record (name, club, class, chip, start times) | ✓ |
| `DbChangeChipNrByStartNr` | Write SI chip number back to DBISAM | ✓ |
| `DbChangeKatNrByStartNr` | Write category (class) number back to DBISAM | ✓ |
| `DbChangeStartTimeByStartNr` | Write start time back to DBISAM | ✓ |
| `DbGetClubList` / `DbGetClubInfo` | Enumerate clubs for Push Clubs | ✓ |
| `DbUpdateName` | Update participant name (available via DB Explorer) | ✓ |
| `DbChangeDnsByStartNr` | Write DNS status back to DBISAM | ⚠ stub — not yet in DLL |

`DbBridge.dll` must be present alongside the executable. Log files written by the DLL are shown in error messages for diagnostics.

**TeilnInfo buffer format** (key=value lines returned by `DbGetTeilnInfoByIdNr`):

| Key | Meaning |
|-----|---------|
| `Name` | Surname |
| `Vorname` | First name |
| `Grupa` | Class/category name |
| `KatNr` | Class ID |
| `Klubs` | Club name |
| `ClubNr` | Club ID |
| `ChipNr1`/`2`/`3` | SI chip numbers |
| `Start1`/`2`/`3` | Start time for stage 1/2/3 (`HH:mm:ss`) |

---

## Running

```bash
cd Desktop
dotnet run
```

Or build a self-contained executable:

```bash
dotnet publish -c Release -r win-x86 --self-contained
```

Place `DbBridge.dll` in the same folder as the built executable.

---

## Stack

- .NET 8 WinForms (`net8.0-windows`)
- `System.Net.Http.Json` for API calls
- `System.Text.Json` for settings persistence
- `System.Runtime.InteropServices` (P/Invoke) for DbBridge DLL

---

## Project layout

```
Desktop/
  Program.cs                       Entry point
  AppSettings.cs                   Settings model + JSON persistence
  Forms/
    MainForm.cs/.Designer.cs       Main UI and button logic
    SettingsDialog.cs              Edit API/key/device settings
    PullPastDialog.cs              Time-window picker for Pull Past
    DbBridgeExplorerForm.cs/.Designer.cs  Ad-hoc DLL explorer UI
  Services/
    ApiClient.cs                   GET runners, PUT bulk upload, GET/PUT lookups
    SyncService.cs                 Orchestrates sync cycle/timer/watermark
    DbIsamRepository.cs            DBISAM scan + DB write-back operations
  Models/
    RunnerDto.cs
    GetRunnersResponse.cs
    BulkUploadRequest.cs
    BulkRunnerDto.cs
    BulkUploadResponse.cs
    LookupItemDto.cs
    UpsertLookupRequest.cs
    UpsertLookupResponse.cs
    LookupCountsResponse.cs
  DbBridge/
    DbBridgeNative.cs              P/Invoke declarations for DbBridge.dll
    DbBridgeService.cs             High-level wrapper over DbBridgeNative
    DbBridgeResult.cs              Result record for bridge operations
    DbEtapInfo.cs                  Stage info record from DbGetEtapInfo
  app.manifest
```
