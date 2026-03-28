package com.orienteering.startref.data.sync

import android.content.Context
import androidx.hilt.work.HiltWorker
import androidx.work.CoroutineWorker
import androidx.work.WorkerParameters
import com.orienteering.startref.data.local.PendingSyncDao
import com.orienteering.startref.data.remote.ApiClient
import com.orienteering.startref.data.settings.SettingsDataStore
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
    private val settingsDataStore: SettingsDataStore
) : CoroutineWorker(context, params) {

    override suspend fun doWork(): Result {
        return try {
            val settings = settingsDataStore.settings.first()
            val pending = pendingSyncDao.getPending()
            var allSucceeded = true

            for (entity in pending) {
                val body = JSONObject(entity.payload)
                val success = apiClient.patchRunner(
                    date = entity.competitionDate,
                    startNumber = entity.startNumber,
                    statusId = body.optInt("statusId").takeIf { body.has("statusId") },
                    siCard = body.optString("siChipNo").takeIf { body.has("siChipNo") },
                    name = body.optString("name").takeIf { body.has("name") },
                    surname = body.optString("surname").takeIf { body.has("surname") },
                    classId = body.optInt("classId").takeIf { body.has("classId") },
                    clubId = body.optInt("clubId").takeIf { body.has("clubId") },
                    country = body.optString("country").takeIf { body.has("country") },
                    startPlace = body.optInt("startPlace").takeIf { body.has("startPlace") },
                    startTime = body.optString("startTime").takeIf { body.has("startTime") },
                    lastModifiedAtMs = body.getLong("lastModifiedAtMs"),
                    source = settings.deviceName,
                    settings = settings
                )
                if (success) pendingSyncDao.markSent(entity.id)
                else {
                    pendingSyncDao.markFailed(entity.id)
                    allSucceeded = false
                }
            }

            if (allSucceeded) Result.success()
            else if (runAttemptCount < 3) Result.retry()
            else Result.failure()
        } catch (e: Exception) {
            if (runAttemptCount < 3) Result.retry() else Result.failure()
        }
    }
}
