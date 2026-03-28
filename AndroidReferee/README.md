# StartRef — Android Referee App

Android app for orienteering **start referees** and **gate operators** to manage runner statuses in real time during an event. Part of the StartRef system alongside the .NET API and Windows Desktop app.

## Purpose

At an orienteering event the app serves two roles:

- **Start Referee view** — displays the full start list, lets referees mark runners as Started or DNS, edit runner details, and view the currently active start minute
- **Gate view** — reads SI chip numbers via a USB OTG SportIdent station, automatically matches cards to runners in the current minute, and signals the result with colour coding

All changes are patched to the central StartRef API and synced back from it every ~30 seconds (delta sync).

---

## Business Rules

### Status model

| StatusId | Name       | Who sets it |
|----------|------------|-------------|
| 1        | Registered | Default     |
| 2        | Started    | Android (gate chip read or referee tap) |
| 3        | DNS        | Android referee, or Desktop (from OE12) |

Status transitions are **forward-only**: once a runner is Started or DNS they can never revert to Registered via this app.

### Start list source

- Loaded from the StartRef API
- Persisted locally in Room DB; the app works fully offline after the first sync

### Delta sync

- Every ~30 seconds: `GET /api/competitions/{today}/runners?changedSince={watermark}`
- Applied fields from server: name, surname, siChipNo, clubName, country, startPlace, statusId
- `className` is **never** updated from the server — it is immutable after initial upload
- Watermark (`serverTimeUtc`) is stored in DataStore

### Pending sync queue

- Every status change or edit is immediately PATCHed to the API
- On failure it is queued in the local `pending_sync` table and retried by WorkManager

### Gate screen — colour signals

| Scenario | Time-field colour | Action |
|----------|------------------|--------|
| Idle | White | — |
| SI card matches runner in current minute | Bright green → green | Runner marked Started, PATCH queued |
| SI card matches runner within ±5 min | Orange | Approve / Handle Manually |
| SI card not found | Red | Tap a runner row to assign chip, or Handle Manually |
| Approve tapped (orange) | Green | Runner marked Started, PATCH queued |
| Handle Manually tapped | White | Event logged locally, no status change |

### SI station integration

- USB OTG serial via [usb-serial-for-android](https://github.com/mik3y/usb-serial-for-android)
- SportIdent BSM-7/8 station at 38400 baud (8N1)
- USB permission requested at runtime; device filter targets FTDI FT232R (VID 0x0403, PID 0x6001)
- Reader starts when Gate screen is active, stops when navigated away

---

## Settings

| Setting | Description |
|---------|-------------|
| API Base URL | StartRef API base URL |
| API Key | `X-Api-Key` header value for mutations |
| Competition Date | `yyyy-MM-dd` date used for all API calls (defaults to today) |
| Device Name | Identifier sent as `lastModifiedBy` on every PATCH |
| Event header | Title shown in the toolbar |
| Prestart minutes | Minutes before start the runner is at the line (negative = early) |
| Late start minutes | Global event delay in minutes |
| Sound / Vibration alert | Triggers at the start of each minute |
| Row font size | Adjustable 12–28 sp |
| Clear cache | Deletes all local runner data and sync history |

---

## Stack

- Kotlin + Jetpack Compose + Material 3
- Hilt (dependency injection)
- Room DB v3 (local persistence with migrations v1→2→3)
- DataStore Preferences (settings + sync watermark)
- WorkManager (pending PATCH retry)
- OkHttp (API calls)
- usb-serial-for-android (USB OTG serial, from JitPack)
- `java.time` for all date/time handling

## Minimum SDK

API 26 (Android 8.0)

---

## Project layout

```
app/src/main/java/com/orienteering/startref/
  data/
    local/          Room entities (RunnerEntity v3, PendingSyncEntity), DAOs, AppDatabase
    remote/         ApiClient (PATCH + GET)
    repository/     StartListRepository
    settings/       AppSettings, SettingsDataStore
    si/             SiStationReader (USB OTG SportIdent)
    sync/           SyncManager (30s polling), PendingSyncWorker
  di/               Hilt AppModule
  ui/
    startlist/      StartListScreen, StartListViewModel, components
    gate/           GateScreen, GateViewModel
    edituser/       EditUserDialog
    settings/       SettingsScreen, SettingsViewModel
    navigation/     AppNavigation (bottom tabs: Start List | Gate)
  StartRefApp.kt    Application class; launches SyncManager polling loop
```

---

## Run

1. Open `AndroidReferee/` in Android Studio.
2. Sync Gradle (JitPack is configured in `settings.gradle.kts`).
3. Run `app` on device or emulator (portrait orientation).

```bash
./gradlew assembleDebug
```

Initial load and all subsequent syncs come from the StartRef API.
