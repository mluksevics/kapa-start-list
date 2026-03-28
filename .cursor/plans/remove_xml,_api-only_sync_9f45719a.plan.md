---
name: Remove XML, API-only sync
overview: Remove all XML-based data loading from Android. All runner data — including initial load — comes exclusively from the API via `SyncManager`. The "Reload startlist" button triggers a full API sync instead of XML fetch.
todos:
  - id: delete-xml-files
    content: Delete XmlStartListParser.kt and assets/startlis.xml
    status: completed
  - id: sync-manager-fullsync
    content: Add fullSync() to SyncManager.kt
    status: completed
  - id: repository-rewire
    content: "Rewire StartListRepository: remove xmlParser, inject SyncManager, add reloadFromApi(), remove XML methods"
    status: completed
  - id: dao-remove-xml-query
    content: Remove updateXmlFields() from RunnerDao.kt
    status: completed
  - id: settings-cleanup
    content: Remove xmlUrl from AppSettings, SettingsDataStore, SettingsViewModel, and SettingsScreen
    status: completed
  - id: viewmodels-rewire
    content: Update reloadStartList() in both SettingsViewModel and StartListViewModel to call reloadFromApi()
    status: completed
isProject: false
---

# Remove XML, API-only Android Sync

## What changes

### Delete

- `[XmlStartListParser.kt](AndroidReferee/app/src/main/java/com/orienteering/startref/data/remote/XmlStartListParser.kt)` — entire XML parser
- `[assets/startlis.xml](AndroidReferee/app/src/main/assets/startlis.xml)` — bundled sample data

### `SyncManager.kt`

Add `fullSync()` — same logic as `poll()` but always passes `changedSince = null` (full fetch, ignores watermark). Rename the private `mergeRunner` visibility or keep as-is; `fullSync` is the public entry point.

```kotlin
suspend fun fullSync() {
    val settings = settingsDataStore.settings.first()
    val result = apiClient.getRunners(settings.competitionDate, null, settings) ?: return
    result.runners.forEach { dto -> mergeRunner(dto, settings.competitionDate) }
    settingsDataStore.updateLastServerTimeUtc(result.serverTimeUtc)
}
```

### `StartListRepository.kt`

- Remove `XmlStartListParser` import and constructor param
- Inject `SyncManager` instead
- Remove `loadFromAsset()`, `reloadFromXml()`, `mergeRunners()` methods
- Add `reloadFromApi()` that calls `syncManager.fullSync()`

### `RunnerDao.kt`

- Remove `updateXmlFields()` query — only used by the now-deleted XML merge path

### `AppSettings.kt`

- Remove `xmlUrl` field and its default value

### `SettingsDataStore.kt`

- Remove `XML_URL` key, `xmlUrl` mapping, `updateXmlUrl()` method

### `SettingsViewModel.kt`

- Remove `updateXmlUrl()`, `loadSampleData()` 
- `reloadStartList()` → calls `repository.reloadFromApi()` instead of `reloadFromXml()`

### `StartListViewModel.kt`

- `reloadStartList()` → calls `repository.reloadFromApi()` instead of `reloadFromXml(settings.value.xmlUrl)`

### `SettingsScreen.kt`

- Remove local `xmlUrl` state variable and `LaunchedEffect` init for it
- Remove "Start list location" `OutlinedTextField` block
- Remove "Load sample data (startlis.xml)" `OutlinedButton`

