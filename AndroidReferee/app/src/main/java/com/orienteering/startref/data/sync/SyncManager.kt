package com.orienteering.startref.data.sync

import com.orienteering.startref.data.local.RunnerDao
import com.orienteering.startref.data.local.entity.RunnerEntity
import com.orienteering.startref.data.remote.ApiClient
import com.orienteering.startref.data.remote.RunnerDto
import com.orienteering.startref.data.settings.SettingsDataStore
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.first
import javax.inject.Inject
import javax.inject.Singleton

private const val POLL_INTERVAL_MS = 30_000L

@Singleton
class SyncManager @Inject constructor(
    private val runnerDao: RunnerDao,
    private val apiClient: ApiClient,
    private val settingsDataStore: SettingsDataStore
) {
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

        result.runners.forEach { dto -> mergeRunner(dto) }
        settingsDataStore.updateLastServerTimeUtc(result.serverTimeUtc)
    }

    private suspend fun mergeRunner(dto: RunnerDto) {
        val existing = runnerDao.getByStartNumber(dto.startNumber)
        if (existing == null) {
            runnerDao.insert(dto.toEntity())
            return
        }

        // Merge all fields except className — className is immutable after initial upload.
        // Status: forward-only transitions only.
        val resolvedStatus = resolveStatus(incoming = dto.statusId, current = existing.statusId)
        val updated = existing.copy(
            siCard = dto.siChipNo ?: existing.siCard,
            name = dto.name,
            surname = dto.surname,
            clubName = dto.clubName,
            country = dto.country ?: existing.country,
            startPlace = dto.startPlace,
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

    private fun RunnerDto.toEntity() = RunnerEntity(
        startNumber = startNumber,
        siCard = siChipNo ?: "",
        name = name,
        surname = surname,
        className = className,
        clubName = clubName,
        startTime = 0L,   // not provided by API; will be set from XML import if available
        country = country ?: "",
        startPlace = startPlace,
        statusId = statusId,
        lastModifiedAt = lastModifiedUtc,
        lastModifiedBy = lastModifiedBy
    )
}
