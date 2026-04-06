package com.orienteering.startref.data.sync

import android.content.Context
import androidx.hilt.work.HiltWorker
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import com.orienteering.startref.data.local.PendingSyncDao
import com.orienteering.startref.data.remote.ApiClient
import com.orienteering.startref.data.settings.SettingsDataStore
import com.orienteering.startref.data.si.SiDebugLog
import dagger.assisted.Assisted
import dagger.assisted.AssistedInject
import kotlinx.coroutines.flow.first
import org.json.JSONObject

@HiltWorker
class PendingSyncWorker @AssistedInject constructor(
    @Assisted context: Context,
    @Assisted params: WorkerParameters,
    private val pendingSyncDao: PendingSyncDao,
    private val apiClient: ApiClient,
    private val settingsDataStore: SettingsDataStore,
    private val log: SiDebugLog
) : CoroutineWorker(context, params) {

    override suspend fun doWork(): Result {
        return try {
            val settings = settingsDataStore.settings.first()
            val pending = pendingSyncDao.getPending()
            var allSucceeded = true

            log.log("Push: ${pending.size} pending update(s)")
            for (entity in pending) {
                val body = JSONObject(entity.payload)
                val error = apiClient.patchRunner(
                    date = entity.competitionDate,
                    startNumber = entity.startNumber,
                    statusId = body.optInt("statusId").takeIf { body.has("statusId") },
                    siCard = body.optString("siChipNo").takeIf { body.has("siChipNo") },
                    name = body.optString("name").takeIf { body.has("name") },
                    surname = body.optString("surname").takeIf { body.has("surname") },
                    classId = body.optInt("classId").takeIf { body.has("classId") },
                    clubId = body.optInt("clubId").takeIf { body.has("clubId") },
                    startTime = body.optString("startTime").takeIf { body.has("startTime") },
                    lastModifiedAtMs = body.getLong("lastModifiedAtMs"),
                    source = settings.deviceName,
                    settings = settings
                )
                if (error == null) {
                    pendingSyncDao.markSent(entity.id)
                    log.log("Push #${entity.startNumber} OK")
                } else {
                    pendingSyncDao.markFailed(entity.id)
                    log.log("Push #${entity.startNumber} FAILED: $error (attempt ${runAttemptCount + 1})")
                    allSucceeded = false
                }
            }

            if (allSucceeded) Result.success()
            else if (runAttemptCount < 3) Result.retry()
            else Result.failure()
        } catch (e: Exception) {
            log.log("Push exception: ${e.message}")
            if (runAttemptCount < 3) Result.retry() else Result.failure()
        }
    }
}
