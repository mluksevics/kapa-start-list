package com.orienteering.startref.data.repository

import android.content.ContentValues
import android.content.Context
import android.os.Build
import android.os.Environment
import android.provider.MediaStore
import com.orienteering.startref.data.local.PendingSyncDao
import com.orienteering.startref.data.local.RunnerDao
import com.orienteering.startref.data.local.entity.PendingSyncEntity
import com.orienteering.startref.data.local.entity.RunnerEntity
import com.orienteering.startref.data.remote.AzureServiceBusClient
import com.orienteering.startref.data.remote.XmlStartListParser
import com.orienteering.startref.data.settings.AppSettings
import com.orienteering.startref.data.settings.SettingsDataStore
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.first
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
    private val pendingSyncDao: PendingSyncDao,
    private val xmlParser: XmlStartListParser,
    private val azureClient: AzureServiceBusClient,
    private val settingsDataStore: SettingsDataStore,
    @ApplicationContext private val context: Context
) {
    fun observeRunners(): Flow<List<RunnerEntity>> = runnerDao.observeAll()

    fun observeClasses(): Flow<List<String>> = runnerDao.observeDistinctClasses()

    fun observeSyncCounts(): Flow<Pair<Int, Int>> = combine(
        pendingSyncDao.observeSentCount(),
        pendingSyncDao.observeTotalCount()
    ) { sent, total -> sent to total }

    suspend fun loadFromAsset(assetName: String) {
        val newRunners = xmlParser.parseFromAsset(context, assetName)
        mergeRunners(newRunners)
    }

    suspend fun reloadFromXml(url: String) {
        val newRunners = xmlParser.fetchAndParse(url)
        mergeRunners(newRunners)
    }

    private suspend fun mergeRunners(newRunners: List<RunnerEntity>) {
        newRunners.forEach { runner ->
            val existing = runnerDao.getByStartNumber(runner.startNumber)
            if (existing != null) {
                runnerDao.updateXmlFields(
                    startNumber = runner.startNumber,
                    name = runner.name,
                    surname = runner.surname,
                    siCard = runner.siCard,
                    className = runner.className,
                    clubName = runner.clubName,
                    startTime = runner.startTime
                )
            } else {
                runnerDao.insert(runner)
            }
        }
    }

    suspend fun toggleCheckIn(startNumber: Int) {
        val runner = runnerDao.getByStartNumber(startNumber) ?: return
        val updated = runner.copy(
            checkedIn = !runner.checkedIn,
            checkedInAt = if (!runner.checkedIn) System.currentTimeMillis() else null
        )
        runnerDao.update(updated)
        enqueueAndPush(PendingSyncEntity.TYPE_CHECK_IN, buildRunnerPayload(updated))
    }

    suspend fun toggleDns(startNumber: Int) {
        val runner = runnerDao.getByStartNumber(startNumber) ?: return
        val updated = runner.copy(dns = !runner.dns)
        runnerDao.update(updated)
        enqueueAndPush(PendingSyncEntity.TYPE_DNS, buildRunnerPayload(updated))
    }

    suspend fun updateRunner(runner: RunnerEntity) {
        runnerDao.update(runner)
        enqueueAndPush(PendingSyncEntity.TYPE_EDIT, buildRunnerPayload(runner))
    }

    suspend fun clearAllData() {
        runnerDao.deleteAll()
        pendingSyncDao.deleteAll()
    }

    suspend fun exportToCsv() {
        val runners = runnerDao.getAll()
        val csv = buildString {
            appendLine("StartNumber,Name,Surname,SICard,Class,Club,StartTime,CheckedIn")
            runners.forEach { r ->
                val timeStr = epochToIso(r.startTime)
                appendLine("${r.startNumber},\"${r.name}\",\"${r.surname}\",${r.siCard},\"${r.className}\",\"${r.clubName}\",$timeStr,${r.checkedIn}")
            }
        }
        writeToDownloads("startlist_${System.currentTimeMillis()}.csv", csv)
    }

    suspend fun pushAllPending() {
        val settings = settingsDataStore.settings.first()
        pendingSyncDao.getPending().forEach { entity ->
            val success = azureClient.sendMessage(entity.payload, settings)
            if (success) pendingSyncDao.markSent(entity.id)
            else pendingSyncDao.markFailed(entity.id)
        }
    }

    private suspend fun enqueueAndPush(type: String, payload: String) {
        val id = pendingSyncDao.insert(PendingSyncEntity(type = type, payload = payload))
        val settings = settingsDataStore.settings.first()
        val success = azureClient.sendMessage(payload, settings)
        if (success) pendingSyncDao.markSent(id)
        else pendingSyncDao.markFailed(id)
    }

    private fun buildRunnerPayload(runner: RunnerEntity): String {
        return JSONObject().apply {
            put("startNumber", runner.startNumber)
            put("name", runner.name)
            put("surname", runner.surname)
            put("siCard", runner.siCard)
            put("className", runner.className)
            put("clubName", runner.clubName)
            put("startTime", epochToIso(runner.startTime))
            put("checkedIn", runner.checkedIn)
            runner.checkedInAt?.let { put("checkedInAt", epochToIso(it)) }
            put("timestamp", System.currentTimeMillis())
        }.toString()
    }

    private fun epochToIso(epochMs: Long): String {
        return DateTimeFormatter.ISO_OFFSET_DATE_TIME
            .format(Instant.ofEpochMilli(epochMs).atZone(ZoneId.systemDefault()))
    }

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
