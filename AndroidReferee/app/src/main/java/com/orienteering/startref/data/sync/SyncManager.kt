package com.orienteering.startref.data.sync

import com.orienteering.startref.data.local.RunnerDao
import com.orienteering.startref.data.local.entity.RunnerEntity
import com.orienteering.startref.data.remote.ApiClient
import com.orienteering.startref.data.remote.RunnerDto
import com.orienteering.startref.data.settings.SettingsDataStore
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.asSharedFlow
import java.time.LocalDate
import java.time.LocalTime
import java.time.ZoneId
import javax.inject.Inject
import javax.inject.Singleton

private const val POLL_INTERVAL_MS = 30_000L

@Singleton
class SyncManager @Inject constructor(
    private val runnerDao: RunnerDao,
    private val apiClient: ApiClient,
    private val settingsDataStore: SettingsDataStore
) {
    data class SyncDelta(
        val runnersChanged: Int,
        val classNamesChanged: Int,
        val clubNamesChanged: Int
    )

    private val _syncDeltas = MutableSharedFlow<SyncDelta>(extraBufferCapacity = 16)
    val syncDeltas: SharedFlow<SyncDelta> = _syncDeltas.asSharedFlow()

    /** Runs indefinitely — launch in a long-lived coroutine scope (e.g. Application scope). */
    suspend fun startPolling() {
        while (true) {
            runCatching { poll() }
            delay(POLL_INTERVAL_MS)
        }
    }

    suspend fun poll() {
        val settings = settingsDataStore.settings.first()
        val changedSince = settings.lastServerTimeUtc.takeIf { it > 0 }
        val result = apiClient.getRunners(settings.competitionDate, changedSince, settings) ?: return

        var runnersChanged = 0
        result.runners.forEach { dto -> mergeRunner(dto, settings.competitionDate) }
        runnersChanged = result.runners.size
        val classNamesChanged = syncClassLookups(settings)
        val clubNamesChanged = syncClubLookups(settings)
        settingsDataStore.updateLastServerTimeUtc(result.serverTimeUtc)
        if (runnersChanged > 0 || classNamesChanged > 0 || clubNamesChanged > 0) {
            _syncDeltas.tryEmit(
                SyncDelta(
                    runnersChanged = runnersChanged,
                    classNamesChanged = classNamesChanged,
                    clubNamesChanged = clubNamesChanged
                )
            )
        }
    }

    suspend fun fullSync() {
        val settings = settingsDataStore.settings.first()
        val result = apiClient.getRunners(settings.competitionDate, null, settings) ?: return

        result.runners.forEach { dto -> mergeRunner(dto, settings.competitionDate) }
        val classNamesChanged = syncClassLookups(settings)
        val clubNamesChanged = syncClubLookups(settings)
        settingsDataStore.updateLastServerTimeUtc(result.serverTimeUtc)
        _syncDeltas.tryEmit(
            SyncDelta(
                runnersChanged = result.runners.size,
                classNamesChanged = classNamesChanged,
                clubNamesChanged = clubNamesChanged
            )
        )
    }

    private suspend fun mergeRunner(dto: RunnerDto, competitionDate: String) {
        val existing = runnerDao.getByStartNumber(dto.startNumber)
        if (existing == null) {
            runnerDao.insert(dto.toEntity(competitionDate))
            return
        }

        // Status: forward-only transitions only.
        val resolvedStatus = resolveStatus(incoming = dto.statusId, current = existing.statusId)
        val updated = existing.copy(
            siCard = dto.siChipNo ?: existing.siCard,
            name = dto.name,
            surname = dto.surname,
            classId = dto.classId,
            className = dto.className,
            clubId = dto.clubId,
            clubName = dto.clubName,
            country = dto.country ?: existing.country,
            startPlace = dto.startPlace,
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

    /**
     * Forward-only status transitions:
     * - Never downgrade Started(2) or DNS(3) back to Registered(1)
     * - DNS(3) can override Started(2)
     * - Started(2) can override Registered(1)
     */
    private fun resolveStatus(incoming: Int, current: Int): Int {
        if (incoming == 1 && current in listOf(2, 3)) return current
        if (incoming == 3) return 3
        if (incoming == 2 && current == 1) return 2
        return current
    }

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
        country = country ?: "",
        startPlace = startPlace,
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
        var changed = 0
        classes.forEach { item ->
            changed += runnerDao.updateClassNameByClassId(item.id, item.name)
        }
        return changed
    }

    private suspend fun syncClubLookups(settings: com.orienteering.startref.data.settings.AppSettings): Int {
        val clubs = apiClient.getClubs(settings)
        if (clubs.isEmpty()) return 0
        var changed = 0
        clubs.forEach { item ->
            changed += runnerDao.updateClubNameByClubId(item.id, item.name)
        }
        return changed
    }
}
