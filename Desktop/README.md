# StartRef Desktop

.NET 8 WinForms application that runs on the **event PC** alongside OE12. It performs bidirectional synchronisation between the OE12 DBISAM database and the StartRef API, so that changes made by field Android devices (SI chip assignments, category corrections, start times) are written back to DBISAM, and new or corrected runner data from OE12 can be pushed up to the API.

---

## Sync cycle

Each regular cycle (auto or manual) runs these steps in order:

```
1. PULL    GET /api/competitions/{today}/runners?changedSince={watermark}
           → receive all runner changes from field devices since last sync

2. WRITE   For each changed runner (not modified by this desktop device), using API `changedFields` when provided:
             • ChipNr   → DbChangeChipNrByStartNr (only if `SiChipNo` is in `changedFields`, or if `changedFields` is omitted/null — legacy behaviour)
             • KatNr    → DbChangeKatNrByStartNr (`ClassId`)
             • StartTime → DbChangeStartTimeByStartNr (`StartTime`)
             Name/club diagnostic logs respect the same hints. Omitted/null `changedFields` means treat all writable fields as candidates (value-compare against DBISAM as before).
           (DNS write-back is pending DLL support for DbChangeDnsByStartNr)

3. WATERMARK  Save serverTimeUtc from the response for the next delta pull
```

Implementation split:

- `Services/SyncService.cs` orchestrates API pull/push, timer, and watermark
- `Services/DbIsamRepository.cs` handles DBISAM scan + write-back logic
- `DbBridge/DbBridgeService.cs` wraps `DbBridgeNative` P/Invoke calls

**Force Push All** adds a separate phase after the regular cycle:

```
4. SCAN    Iterate start numbers 1–4000 via GetIdNrListByStartNr + GetTeilnInfoByIdNr
           → read full runner data from DBISAM (name, category, club, chip, start time)

5. PUSH    PUT /api/competitions/{today}/runners  (bulk upsert, lastModifiedUtc = now)
           → overwrites all non-status fields on the API; status escalation rules still apply
```

**Push selected** (inclusive *from*…*to* start numbers): same flow as Force Push All, but the DBISAM scan is limited to the chosen range (still clamped to 1–4000). Opens a dialog for *From* / *To* bib numbers.

---

## Buttons

| Button | Behaviour |
|--------|-----------|
| **Pull Now** | Runs one regular cycle immediately (PULL → write back to DBISAM) |
| **Push all changes** | Sync first, then bulk-uploads eligible DBISAM rows; API updates only rows that actually differ |
| **Push Clubs** | Reads clubs from DBISAM and upserts them to the API `/api/lookups/clubs` table |
| **Push Classes** | Reads classes from DBISAM and upserts them to the API `/api/lookups/classes` table |
| **Pull Past** | Opens a dialog to pull changes from the last N minutes (10/15/30/60/240 or custom). Useful after a connectivity gap. |
| **Peek API data** | Reads current API SQL row counts (competitors/clubs/classes) and logs them |
| **Advanced** (▶ collapsed by default) | Expands to show **Force Push All**, **Push selected**, **Delete Today**, **Upload new**, and **E** (DB Explorer — ad-hoc DbBridge DLL calls and diagnostics) |
| **Cancel** | Below the Advanced row; on the left of the status line — cancels the currently running operation |
| **Force Push All** | Regular sync first, then scans DBISAM 1–4000 and bulk-uploads everything. Use after correcting data in OE12. |
| **Push selected** | Regular sync first, then scans only an inclusive start-number range from DBISAM and bulk-uploads those rows. |
| **Delete Today** | Deletes all competition data for the selected API date (with confirmation) |
| **Upload new** | Uploads DBISAM runners missing on the API or changed vs Registered/DNS |

At app startup, **Peek API data** runs automatically once.

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

| DLL function | Purpose |
|---|---|
| `DbOpen` / `DbClose` | Open/close the DBISAM folder |
| `DbGetEtapInfo` | Read stage name and date for the day selector |
| `DbGetIdNrListByStartNr` | Look up internal IdNr(s) for a start number |
| `DbGetTeilnInfoByIdNr` | Read full participant record (name, club, class, chip, start times) |
| `DbChangeChipNrByStartNr` | Write SI chip number back to DBISAM |
| `DbChangeKatNrByStartNr` | Write category (class) number back to DBISAM |
| `DbChangeStartTimeByStartNr` | Write start time back to DBISAM |
| `DbGetClubList` / `DbGetClubInfo` | Enumerate clubs for Push Clubs |
| `DbUpdateName` | Update participant name (available via DB Explorer) |

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
cd Desktop/StartRef.Desktop
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
Desktop/StartRef.Desktop/
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
