package com.orienteering.startref.data.sync

import androidx.room.withTransaction
import com.orienteering.startref.data.local.AppDatabase
import com.orienteering.startref.data.local.LookupDao
import com.orienteering.startref.data.local.RunnerDao
import com.orienteering.startref.data.local.entity.ClassLookupEntity
import com.orienteering.startref.data.local.entity.ClubLookupEntity
import com.orienteering.startref.data.local.entity.RunnerEntity
import com.orienteering.startref.data.remote.ApiClient
import com.orienteering.startref.data.remote.RunnerDto
import com.orienteering.startref.data.settings.SettingsDataStore
import com.orienteering.startref.data.si.SiDebugLog
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asSharedFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.delay
import java.time.LocalDate
import java.time.LocalTime
import java.time.ZoneId
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class SyncManager @Inject constructor(
    private val db: AppDatabase,
    private val runnerDao: RunnerDao,
    private val lookupDao: LookupDao,
    private val apiClient: ApiClient,
    private val settingsDataStore: SettingsDataStore,
    private val log: SiDebugLog
) {
    data class SyncDelta(
        val runnersChanged: Int,
        val classNamesChanged: Int,
        val clubNamesChanged: Int,
        val runnerFieldHighlights: Map<Int, Set<String>> = emptyMap()
    )

    private val _syncDeltas = MutableSharedFlow<SyncDelta>(extraBufferCapacity = 16)
    val syncDeltas: SharedFlow<SyncDelta> = _syncDeltas.asSharedFlow()
    private val _isSyncing = MutableStateFlow(false)
    val isSyncing: StateFlow<Boolean> = _isSyncing.asStateFlow()

    /** Runs indefinitely — launch in a long-lived coroutine scope (e.g. Application scope). */
    suspend fun startPolling() {
        while (true) {
            runCatching { poll() }
            val intervalMs = settingsDataStore.settings.first().pollIntervalSeconds
                .coerceAtLeast(5)
                .times(1000L)
            delay(intervalMs)
        }
    }

    suspend fun poll() {
        _isSyncing.value = true
        try {
            val settings = settingsDataStore.settings.first()
            val changedSince = settings.lastServerTimeUtc.takeIf { it > 0 }
            log.log("Poll: GET runners (changedSince=${if (changedSince != null) "yes" else "full"})")
            val result = apiClient.getRunners(settings.competitionDate, changedSince, settings) ?: run {
                log.log("Poll: no response from server")
                return
            }

            val runnersChanged = result.runners.size
            val fieldHighlights = buildMap<Int, MutableSet<String>> {
                result.runners.forEach { dto ->
                    mergeRunner(dto, settings.competitionDate)
                    val cf = dto.changedFields
                    if (!cf.isNullOrEmpty()) {
                        getOrPut(dto.startNumber) { mutableSetOf() }.addAll(cf)
                    }
                }
            }.mapValues { it.value.toSet() }
            val classNamesChanged = syncClassLookups(settings)
            val clubNamesChanged = syncClubLookups(settings)
            settingsDataStore.updateLastServerTimeUtc(result.serverTimeUtc)
            log.log("Poll done: runners=$runnersChanged classes=$classNamesChanged clubs=$clubNamesChanged")
            if (runnersChanged > 0 || classNamesChanged > 0 || clubNamesChanged > 0 || fieldHighlights.isNotEmpty()) {
                _syncDeltas.tryEmit(
                    SyncDelta(
                        runnersChanged = runnersChanged,
                        classNamesChanged = classNamesChanged,
                        clubNamesChanged = clubNamesChanged,
                        runnerFieldHighlights = fieldHighlights
                    )
                )
            }
        } catch (e: Exception) {
            log.log("Poll error: ${e.message}")
        } finally {
            _isSyncing.value = false
        }
    }

    suspend fun fullSync() {
        _isSyncing.value = true
        try {
            val settings = settingsDataStore.settings.first()
            log.log("Full sync: GET all runners")
            val result = apiClient.getRunners(settings.competitionDate, null, settings)
                ?: throw IllegalStateException("No response from API – check API URL and competition date in Settings")

            val entities = result.runners.map { it.toEntity(settings.competitionDate) }
            db.withTransaction {
                runnerDao.deleteAll()
                if (entities.isNotEmpty()) runnerDao.insertAll(entities)
            }

            val classNamesChanged = syncClassLookups(settings)
            val clubNamesChanged = syncClubLookups(settings)
            settingsDataStore.updateLastServerTimeUtc(result.serverTimeUtc)
            log.log("Full sync done: ${entities.size} runners, classes=$classNamesChanged clubs=$clubNamesChanged")
            _syncDeltas.tryEmit(
                SyncDelta(
                    runnersChanged = entities.size,
                    classNamesChanged = classNamesChanged,
                    clubNamesChanged = clubNamesChanged,
                    runnerFieldHighlights = emptyMap()
                )
            )
        } finally {
            _isSyncing.value = false
        }
    }

    suspend fun pullClassesOnly(): Int {
        _isSyncing.value = true
        return try {
            val settings = settingsDataStore.settings.first()
            log.log("Pull classes")
            val n = syncClassLookups(settings)
            log.log("Pull classes done: $n updated")
            n
        } finally {
            _isSyncing.value = false
        }
    }

    suspend fun pullClubsOnly(): Int {
        _isSyncing.value = true
        return try {
            val settings = settingsDataStore.settings.first()
            log.log("Pull clubs")
            val n = syncClubLookups(settings)
            log.log("Pull clubs done: $n updated")
            n
        } finally {
            _isSyncing.value = false
        }
    }

    private suspend fun mergeRunner(dto: RunnerDto, competitionDate: String) {
        val existing = runnerDao.getByStartNumber(dto.startNumber)
        if (existing == null) {
            runnerDao.insert(dto.toEntity(competitionDate))
            return
        }

        // Trust API status on pull — allows Started/DNS to be reversed by referee PATCH.
        val resolvedStatus = resolveStatus(incoming = dto.statusId, current = existing.statusId)
        val updated = existing.copy(
            siCard = dto.siChipNo ?: existing.siCard,
            name = dto.name,
            surname = dto.surname,
            classId = dto.classId,
            className = dto.className,
            clubId = dto.clubId,
            clubName = dto.clubName,
            startTime = dto.startTime?.let { hhmmssToEpochMs(it, competitionDate) } ?: existing.startTime,
            statusId = resolvedStatus,
            checkedInAt = if (resolvedStatus == 2 && existing.statusId != 2) System.currentTimeMillis() else existing.checkedInAt,
            lastModifiedAt = dto.lastModifiedUtc,
            lastModifiedBy = dto.lastModifiedBy
        )

        if (updated != existing) {
            runnerDao.update(updated)
        }
    }

    /** Trust API status on pull so Started/DNS can return to Registered when another device PATCHes. */
    private fun resolveStatus(incoming: Int, current: Int): Int =
        if (incoming in 1..3) incoming else current

    private fun RunnerDto.toEntity(competitionDate: String) = RunnerEntity(
        startNumber = startNumber,
        siCard = siChipNo ?: "",
        name = name,
        surname = surname,
        classId = classId,
        className = className,
        clubId = clubId,
        clubName = clubName,
        startTime = startTime?.let { hhmmssToEpochMs(it, competitionDate) } ?: 0L,
        statusId = statusId,
        lastModifiedAt = lastModifiedUtc,
        lastModifiedBy = lastModifiedBy
    )

    private fun hhmmssToEpochMs(hhmmss: String, competitionDate: String): Long {
        return try {
            val date = LocalDate.parse(competitionDate)
            val time = LocalTime.parse(hhmmss)
            date.atTime(time).atZone(ZoneId.systemDefault()).toInstant().toEpochMilli()
        } catch (_: Exception) {
            0L
        }
    }

    private suspend fun syncClassLookups(settings: com.orienteering.startref.data.settings.AppSettings): Int {
        val classes = apiClient.getClasses(settings)
        if (classes.isEmpty()) return 0
        lookupDao.deleteAllClasses()
        lookupDao.insertClasses(
            classes.map { item ->
                ClassLookupEntity(id = item.id, name = item.name, startPlace = item.startPlace)
            }
        )
        var changed = 0
        classes.forEach { item ->
            changed += runnerDao.updateClassNameByClassId(item.id, item.name)
        }
        return changed
    }

    private suspend fun syncClubLookups(settings: com.orienteering.startref.data.settings.AppSettings): Int {
        val clubs = apiClient.getClubs(settings)
        if (clubs.isEmpty()) return 0
        lookupDao.deleteAllClubs()
        lookupDao.insertClubs(
            clubs.map { item -> ClubLookupEntity(id = item.id, name = item.name) }
        )
        var changed = 0
        clubs.forEach { item ->
            changed += runnerDao.updateClubNameByClubId(item.id, item.name)
        }
        return changed
    }
}
