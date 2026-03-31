package com.orienteering.startref.data.repository

import android.content.ContentValues
import android.content.Context
import android.os.Build
import android.os.Environment
import android.provider.MediaStore
import com.orienteering.startref.data.local.ClassEntry
import com.orienteering.startref.data.local.ClubEntry
import com.orienteering.startref.data.local.LookupDao
import com.orienteering.startref.data.local.PendingSyncDao
import com.orienteering.startref.data.local.RunnerDao
import com.orienteering.startref.data.local.entity.PendingSyncEntity
import com.orienteering.startref.data.local.entity.RunnerEntity
import com.orienteering.startref.data.remote.ApiClient
import com.orienteering.startref.data.settings.SettingsDataStore
import com.orienteering.startref.data.sync.SyncManager
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.map
import org.json.JSONObject
import java.io.File
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class StartListRepository @Inject constructor(
    private val runnerDao: RunnerDao,
    private val lookupDao: LookupDao,
    private val pendingSyncDao: PendingSyncDao,
    private val syncManager: SyncManager,
    private val apiClient: ApiClient,
    private val settingsDataStore: SettingsDataStore,
    @ApplicationContext private val context: Context
) {
    fun observeRunners(): Flow<List<RunnerEntity>> = runnerDao.observeAll()

    fun observeClasses(): Flow<List<ClassEntry>> = runnerDao.observeDistinctClasses()

    fun observeLookupClasses(): Flow<List<ClassEntry>> = lookupDao.observeAllClasses()
        .map { classes -> classes.map { ClassEntry(classId = it.id, className = it.name) } }

    fun observeLookupClubs(): Flow<List<ClubEntry>> = lookupDao.observeAllClubs()
        .map { clubs -> clubs.map { ClubEntry(clubId = it.id, clubName = it.name) } }

    fun observeSyncCounts(): Flow<Pair<Int, Int>> = combine(
        pendingSyncDao.observeSentCount(),
        pendingSyncDao.observeTotalCount()
    ) { sent, total -> sent to total }

    suspend fun reloadFromApi() {
        syncManager.fullSync()
    }

    suspend fun pullClasses() {
        syncManager.pullClassesOnly()
    }

    suspend fun pullClubs() {
        syncManager.pullClubsOnly()
    }

    /** Sets a runner's status to Started (2) and queues a PATCH. */
    suspend fun markStarted(startNumber: Int) = setStatus(startNumber, statusId = 2)

    /** Toggles Started (2) on/off and queues a PATCH. */
    suspend fun toggleStarted(startNumber: Int) {
        val runner = runnerDao.getByStartNumber(startNumber) ?: return
        val newStatusId = if (runner.statusId == 2) 1 else 2
        setStatus(startNumber, newStatusId)
    }

    /** Toggles DNS (3) on/off and queues a PATCH. */
    suspend fun toggleDns(startNumber: Int) {
        val runner = runnerDao.getByStartNumber(startNumber) ?: return
        val newStatusId = if (runner.statusId == 3) 1 else 3
        setStatus(startNumber, newStatusId)
    }

    private suspend fun setStatus(startNumber: Int, statusId: Int) {
        val settings = settingsDataStore.settings.first()
        val runner = runnerDao.getByStartNumber(startNumber) ?: return
        val now = System.currentTimeMillis()
        val updated = runner.copy(
            statusId = statusId,
            checkedInAt = if (statusId == 2) now else runner.checkedInAt,
            lastModifiedAt = now,
            lastModifiedBy = settings.deviceName
        )
        runnerDao.update(updated)
        enqueuePatch(
            type = PendingSyncEntity.TYPE_STATUS,
            competitionDate = settings.competitionDate,
            startNumber = startNumber,
            payload = buildPatchPayload { put("statusId", statusId) },
            lastModifiedAtMs = now,
            settings = settings
        )
    }

    suspend fun updateRunner(runner: RunnerEntity) {
        val settings = settingsDataStore.settings.first()
        val now = System.currentTimeMillis()
        val updated = runner.copy(lastModifiedAt = now, lastModifiedBy = settings.deviceName)
        runnerDao.update(updated)
        enqueuePatch(
            type = PendingSyncEntity.TYPE_EDIT,
            competitionDate = settings.competitionDate,
            startNumber = runner.startNumber,
            payload = buildPatchPayload {
                put("siChipNo", runner.siCard)
                put("name", runner.name)
                put("surname", runner.surname)
                put("classId", runner.classId)
                put("clubId", runner.clubId)
                put("country", runner.country)
                put("startPlace", runner.startPlace)
                if (runner.startTime > 0) put("startTime", epochToHhmmss(runner.startTime))
            },
            lastModifiedAtMs = now,
            settings = settings
        )
    }

    suspend fun clearAllData() {
        runnerDao.deleteAll()
        pendingSyncDao.deleteAll()
    }

    suspend fun exportToCsv() {
        val runners = runnerDao.getAll()
        val csv = buildString {
            appendLine("StartNumber,Name,Surname,SICard,Class,Club,StartTime,StatusId")
            runners.forEach { r ->
                val timeStr = epochToIso(r.startTime)
                appendLine("${r.startNumber},\"${r.name}\",\"${r.surname}\",${r.siCard},\"${r.className}\",\"${r.clubName}\",$timeStr,${r.statusId}")
            }
        }
        writeToDownloads("startlist_${System.currentTimeMillis()}.csv", csv)
    }

    private suspend fun enqueuePatch(
        type: String,
        competitionDate: String,
        startNumber: Int,
        payload: String,
        lastModifiedAtMs: Long,
        settings: com.orienteering.startref.data.settings.AppSettings
    ) {
        val fullPayload = JSONObject(payload).apply {
            put("lastModifiedAtMs", lastModifiedAtMs)
        }.toString()

        val id = pendingSyncDao.insert(
            PendingSyncEntity(
                type = type,
                competitionDate = competitionDate,
                startNumber = startNumber,
                payload = fullPayload
            )
        )

        val body = JSONObject(fullPayload)
        val success = apiClient.patchRunner(
            date = competitionDate,
            startNumber = startNumber,
            statusId = body.optInt("statusId").takeIf { body.has("statusId") },
            siCard = body.optString("siChipNo").takeIf { body.has("siChipNo") },
            name = body.optString("name").takeIf { body.has("name") },
            surname = body.optString("surname").takeIf { body.has("surname") },
            classId = body.optInt("classId").takeIf { body.has("classId") },
            clubId = body.optInt("clubId").takeIf { body.has("clubId") },
            country = body.optString("country").takeIf { body.has("country") },
            startPlace = body.optInt("startPlace").takeIf { body.has("startPlace") },
            startTime = body.optString("startTime").takeIf { body.has("startTime") },
            lastModifiedAtMs = lastModifiedAtMs,
            source = settings.deviceName,
            settings = settings
        )
        if (success) pendingSyncDao.markSent(id)
        else pendingSyncDao.markFailed(id)
    }

    private fun buildPatchPayload(block: JSONObject.() -> Unit): String =
        JSONObject().apply(block).toString()

    private fun epochToIso(epochMs: Long): String =
        DateTimeFormatter.ISO_OFFSET_DATE_TIME
            .format(Instant.ofEpochMilli(epochMs).atZone(ZoneId.systemDefault()))

    private fun epochToHhmmss(epochMs: Long): String =
        DateTimeFormatter.ofPattern("HH:mm:ss")
            .format(Instant.ofEpochMilli(epochMs).atZone(ZoneId.systemDefault()))

    private fun writeToDownloads(fileName: String, content: String) {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            val values = ContentValues().apply {
                put(MediaStore.Downloads.DISPLAY_NAME, fileName)
                put(MediaStore.Downloads.MIME_TYPE, "text/csv")
                put(MediaStore.Downloads.IS_PENDING, 1)
            }
            val resolver = context.contentResolver
            val uri = resolver.insert(MediaStore.Downloads.EXTERNAL_CONTENT_URI, values)
            uri?.let {
                resolver.openOutputStream(it)?.use { stream -> stream.write(content.toByteArray()) }
                values.clear()
                values.put(MediaStore.Downloads.IS_PENDING, 0)
                resolver.update(it, values, null, null)
            }
        } else {
            val dir = Environment.getExternalStoragePublicDirectory(Environment.DIRECTORY_DOWNLOADS)
            File(dir, fileName).writeText(content)
        }
    }
}
