---
name: Integrate DbBridge DLL
overview: Replace non-functional ODBC code with DbBridge.dll P/Invoke as the sole DBISAM access method. Build a testing/exploration UI to probe all DLL functions, then wire into sync service for supported operations.
todos:
  - id: csproj-x86
    content: Add <PlatformTarget>x86</PlatformTarget> to StartRef.Desktop.csproj for 32-bit DLL compatibility
    status: pending
  - id: pinvoke
    content: Create DbBridgeNative.cs with all DllImport declarations and status code constants
    status: pending
  - id: service-wrapper
    content: Create DbBridgeService.cs -- managed wrapper with open/close lifecycle, error handling, typed methods, IDisposable
    status: pending
  - id: explorer-form
    content: "Create DbBridgeExplorerForm -- WinForms dialog with: Open/Close DB, Day selector, buttons for every DLL function, input fields, raw output log"
    status: pending
  - id: mainform-wire
    content: Add 'DbBridge Explorer' button to MainForm that opens the explorer form; update DB path browse to use DbBridge path
    status: pending
  - id: replace-odbc
    content: Replace DbIsamReader/DbIsamWriter ODBC code with DbBridge-based implementations; throw NotSupportedException for bulk read and DNS write
    status: pending
  - id: sync-rework
    content: Rework SyncService -- write ChipNr/StartTime/KatNr from API to DBISAM; skip bulk read and DNS write
    status: pending
isProject: false
---

# Integrate DbBridge DLL into Desktop App

## Why

DBISAM has no ODBC driver on modern systems. The existing `[DbIsamReader`/`DbIsamWriter](Desktop/StartRef.Desktop/DbIsamReader.cs)` ODBC code is non-functional. **DbBridge.dll is the only way** to access DBISAM from C#.

---

## Step 1: Project config

`[StartRef.Desktop.csproj](Desktop/StartRef.Desktop/StartRef.Desktop.csproj)`: add `<PlatformTarget>x86</PlatformTarget>`.

## Step 2: P/Invoke layer

New `Desktop/StartRef.Desktop/DbBridgeNative.cs` -- all DllImport signatures from `[DbBridge/DbBridgeWin32CSharp/NativeMethods.cs](DbBridge/DbBridgeWin32CSharp/NativeMethods.cs)` in `StartRef.Desktop` namespace. All status constants:

```csharp
DBR_OK = 1, DBR_ERROR = 0, DBR_NOT_FOUND = -1,
DBR_INVALID_DAY = -2, DBR_DAY_NOT_ALLOWED = -3,
DBR_INVALID_TIME = -4, DBR_CTX_NIL = -5, DBR_MULTIPLE_MATCHES = -6
```

## Step 3: Managed wrapper

New `Desktop/StartRef.Desktop/DbBridgeService.cs`:

- `IDisposable`, holds `IntPtr _ctx`
- `Open(dataDir)` / `Close()` / `IsOpen` property
- `bool IsAvailable` -- checks if DLL file exists next to EXE
- Typed methods for every DLL function, each calling `DbGetLastError` on failure
- Returns `DbBridgeResult` (success/error code/message) or typed records (`EtapInfo`)
- Logs via `Action<string>` callback

## Step 4: DbBridge Explorer Form (testing UI)

New `Desktop/StartRef.Desktop/DbBridgeExplorerForm.cs` + `.Designer.cs` -- a standalone dialog for manually testing every DLL function and exploring raw data.

### Layout

```
+-- DbBridge Explorer -----------------------------------------+
|                                                               |
|  DB Path: [C:\OE12\data          ] [Browse] [Open] [Close]  |
|  Status: Closed / Open                                       |
|  Day: [1 v]  (NumericUpDown 1-6)                             |
|                                                               |
|  === Read ================================================== |
|  [Get Etap Info]                                              |
|                                                               |
|  IdNr: [____]  [Get Teiln Info]                              |
|  StartNr: [____]  [Get IdNr List by StartNr]                 |
|  ChipNr: [____]  [Get IdNr List by ChipNr]                  |
|                                                               |
|  === Write ================================================= |
|  IdNr: [____]  Time: [hh:mm:ss]  [Change StartTime by IdNr] |
|  StartNr: [____]  Time: [hh:mm:ss]  [Change StartTime by StartNr] |
|  ChipNr: [____]  Time: [hh:mm:ss]  [Change StartTime by ChipNr]  |
|                                                               |
|  IdNr: [____]  NewChip: [____]  [Change ChipNr by IdNr]     |
|  StartNr: [____]  NewChip: [____]  [Change ChipNr by StartNr]|
|  OldChip: [____]  NewChip: [____]  [Change ChipNr by OldChip]|
|                                                               |
|  IdNr: [____]  NewKat: [____]  [Change KatNr by IdNr]       |
|  StartNr: [____]  NewKat: [____]  [Change KatNr by StartNr] |
|  ChipNr: [____]  NewKat: [____]  [Change KatNr by ChipNr]   |
|                                                               |
|  === Test Mode ============================================= |
|  [Enable Test Mode]  [Disable Test Mode]                     |
|                                                               |
|  === Output ================================================ |
|  +--------------------------------------------------------+  |
|  | 10:32:15 | DB OPEN OK                                   |  |
|  | 10:32:18 | GetEtapInfo(1):                              |  |
|  |   Name: Etape 1                                         |  |
|  |   Date: 2026-03-28                                      |  |
|  |   Nullzeit: 36000 → 10:00:00                           |  |
|  | 10:32:25 | GetTeilnInfoByIdNr(42):                      |  |
|  |   <raw buffer contents displayed here>                  |  |
|  +--------------------------------------------------------+  |
|  [Clear Log]                                                  |
+---------------------------------------------------------------+
```

Key design decisions:

- Open/Close is explicit (user controls DB lifecycle)
- Day selector persists across operations (no need to type day for each action)
- Input fields for IdNr, StartNr, ChipNr are reused across read/write sections
- Raw output is displayed verbatim so user can explore `DbGetTeilnInfoByIdNr` format
- All write buttons share the same input fields where possible (e.g. same IdNr field for StartTime/ChipNr/KatNr by IdNr)

### Input field sharing strategy

Three lookup columns, each with its own set of inputs:

- **By IdNr column**: IdNr input, shared across StartTime/ChipNr/KatNr writes + TeilnInfo read
- **By StartNr column**: StartNr input, shared across StartTime/ChipNr/KatNr writes + IdNrList read
- **By ChipNr column**: ChipNr input, shared across StartTime/ChipNr/KatNr writes + IdNrList read

Common inputs: Time (hh:mm:ss), NewChipNr, NewKatNr -- used with whichever lookup column's button is pressed.

## Step 5: Wire into MainForm

`[MainForm.cs](Desktop/StartRef.Desktop/MainForm.cs)` / `[MainForm.Designer.cs](Desktop/StartRef.Desktop/MainForm.Designer.cs)`:

- Add "DbBridge Explorer" button next to existing buttons (opens the explorer form)
- Keep the DB Path browse -- it's now the DBISAM data folder for `DbOpen` (not ODBC)

## Step 6: Replace ODBC code

Rewrite `[DbIsamReader.cs](Desktop/StartRef.Desktop/DbIsamReader.cs)`:

- Remove `System.Data.Odbc` dependency entirely
- `ReadAll()` → `throw new NotSupportedException("DbBridge DLL lacks bulk read. Ask DLL author to add DbReadAllTeiln.")`
- Add `GetRunnerByIdNr(idNr)`, `FindIdNrByStartNr(startNr)`, `FindIdNrByChipNr(dayNo, chipNr)` using DbBridgeService

Rewrite `DbIsamWriter`:

- Remove ODBC code
- `WriteDnsStatuses()` → `throw new NotSupportedException("DbBridge DLL lacks DNS write. Ask DLL author to add DbChangeDnsByStartNr.")`
- Add `WriteChipNr()`, `WriteStartTime()`, `WriteKatNr()` delegating to DbBridgeService

## Step 7: Rework SyncService

`[SyncService.cs](Desktop/StartRef.Desktop/SyncService.cs)`:

- PULL from API still works
- After PULL, write supported field changes (ChipNr, StartTime, KatNr) to DBISAM via DbBridge
- Skip DNS write and bulk read/push with logged warnings
- Keep timer structure

---

## DLL Gaps (for Delphi developer)

For full bidirectional sync, the DLL needs:

1. `**DbReadAllTeiln(ctx, buffer, bufferSize)`** -- enumerate all participants with fields (StartNr, ChipNr, Name, Surname, Class, Club, Country, StartPlace, DNS flag)
2. `**DbChangeDnsByStartNr(ctx, startNr, dnsFlag)`** -- set/clear DNS/AufgabeTyp
3. Or: `**DbExecSQL(ctx, sql, buffer, bufferSize)**` -- raw SQL would solve everything

