# StartRef Desktop

.NET 8 WinForms application that runs on the **event PC** alongside OE12. It performs bidirectional synchronisation between the OE12 DBISAM database and the StartRef API, so that status changes made by field Android devices (Started, DNS) are reflected back in DBISAM — and OE12 edits (corrected names, new runners, DNS entries) are pushed up to the API.

---

## Sync cycle

Each cycle (auto or manual) runs these four steps in order:

```
1. PULL   GET /api/competitions/{today}/runners
          → receive latest statuses from field devices

2. WRITE  For every runner with statusId=3 (DNS) from the API
          → update AufgabeTyp = 'DNS' in DBISAM
          (Started status is NOT written to DBISAM — OE12 manages that separately)

3. READ   Read full start list from DBISAM
          → includes OE12 corrections + DNS just written in step 2

4. PUSH   PUT /api/competitions/{today}/runners  (bulk upsert)
          → API applies status escalation rules; never downgrades Started/DNS
```

---

## Buttons

| Button | Behaviour |
|--------|-----------|
| **Sync Now** | Runs one full cycle immediately (same as auto-sync) |
| **Force Push All** | Sends `lastModifiedUtc = now`, so all non-status fields overwrite the server. Use after correcting data in OE12. Status rules still apply. |

---

## Log

Every change is logged with timestamp, start number, full name, field changed, and `lastModifiedBy` (which device made the change). The log is shown in the UI and simultaneously appended to `sync_log.txt` in the application directory.

---

## Settings

| Setting | Description |
|---------|-------------|
| API Base URL | StartRef API base URL |
| API Key | `X-Api-Key` value for mutation requests |
| DB Path | Path to the DBISAM database folder |
| Device Name | Identifier sent as `lastModifiedBy` on every upload (default: `desktop`) |
| Auto-sync | Enabled/disabled toggle |
| Interval | Sync interval in seconds (default: 60) |

Settings are persisted in `settings.json` next to the executable.

---

## DBISAM setup

`DbIsamReader` and `DbIsamWriter` connect via **ODBC** using the installed DBISAM 4 ODBC driver.

The default query targets typical OE12 column names:

| DBISAM column | Meaning |
|---------------|---------|
| `StartnNr` | Start number |
| `SIKarte` | SI chip number |
| `Vorname` / `Nachname` | First / last name |
| `Kategorie` | Class name |
| `Verein` | Club name |
| `Land` | Country |
| `StartPos` | Start place |
| `AufgabeTyp` | DNS flag (`'DNS'` string) |

**If your OE12 table or column names differ**, edit `DbIsamReader.cs` and `DbIsamWriter.cs` — the SQL queries are simple and clearly labelled.

---

## Running

```bash
cd Desktop/StartRef.Desktop
dotnet run
```

Or build a self-contained executable:

```bash
dotnet publish -c Release -r win-x64 --self-contained
```

---

## Stack

- .NET 8 WinForms (`net8.0-windows`)
- `System.Net.Http.Json` for API calls
- `System.Text.Json` for settings persistence
- `System.Data.Odbc` for DBISAM access
- `System.Windows.Forms.Timer` for the sync interval

---

## Project layout

```
Desktop/StartRef.Desktop/
  Program.cs              Entry point
  MainForm.cs             UI logic
  MainForm.Designer.cs    WinForms layout
  SettingsDialog.cs       Edit API/key/device settings
  AppSettings.cs          Settings model + JSON persistence
  RunnerDto.cs            API request/response DTOs
  ApiClient.cs            GET runners + PUT bulk upload
  DbIsamReader.cs         Read start list from DBISAM via ODBC
  DbIsamWriter.cs         Write DNS statuses back to DBISAM
  SyncService.cs          Full PULL→WRITE→READ→PUSH cycle + timer
  app.manifest
```
