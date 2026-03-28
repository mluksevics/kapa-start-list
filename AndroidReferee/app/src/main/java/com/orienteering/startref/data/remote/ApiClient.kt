package com.orienteering.startref.data.remote

import com.orienteering.startref.data.settings.AppSettings
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import org.json.JSONArray
import org.json.JSONObject
import java.time.Instant
import java.time.ZoneOffset
import java.time.format.DateTimeFormatter

data class RunnerDto(
    val startNumber: Int,
    val siChipNo: String?,
    val name: String,
    val surname: String,
    val className: String,
    val clubName: String,
    val country: String?,
    val statusId: Int,
    val statusName: String,
    val startPlace: Int,
    val lastModifiedUtc: Long,
    val lastModifiedBy: String
)

data class GetRunnersResult(
    val serverTimeUtc: Long,
    val runners: List<RunnerDto>
)

class ApiClient(
    private val okHttpClient: OkHttpClient
) {
    private val isoFormatter = DateTimeFormatter.ISO_OFFSET_DATE_TIME

    private fun toIso(epochMs: Long): String =
        isoFormatter.format(Instant.ofEpochMilli(epochMs).atOffset(ZoneOffset.UTC))

    /** PATCH /api/competitions/{date}/runners/{startNumber} */
    suspend fun patchRunner(
        date: String,
        startNumber: Int,
        statusId: Int?,
        siCard: String?,
        name: String?,
        surname: String?,
        clubName: String?,
        country: String?,
        startPlace: Int?,
        lastModifiedAtMs: Long,
        source: String,
        settings: AppSettings
    ): Boolean = withContext(Dispatchers.IO) {
        val baseUrl = settings.apiBaseUrl.trimEnd('/')
        val apiKey = settings.apiKey
        if (baseUrl.isBlank() || apiKey.isBlank()) return@withContext false

        try {
            val body = JSONObject().apply {
                statusId?.let { put("statusId", it) }
                siCard?.let { put("siChipNo", it) }
                name?.let { put("name", it) }
                surname?.let { put("surname", it) }
                clubName?.let { put("clubName", it) }
                country?.let { put("country", it) }
                startPlace?.let { put("startPlace", it) }
                put("lastModifiedUtc", toIso(lastModifiedAtMs))
                put("source", source)
            }.toString()

            val request = Request.Builder()
                .url("$baseUrl/api/competitions/$date/runners/$startNumber")
                .addHeader("X-Api-Key", apiKey)
                .method("PATCH", body.toRequestBody("application/json".toMediaType()))
                .build()

            okHttpClient.newCall(request).execute().use { it.isSuccessful }
        } catch (_: Exception) {
            false
        }
    }

    /** GET /api/competitions/{date}/runners?changedSince=ISO */
    suspend fun getRunners(
        date: String,
        changedSinceMs: Long?,
        settings: AppSettings
    ): GetRunnersResult? = withContext(Dispatchers.IO) {
        val baseUrl = settings.apiBaseUrl.trimEnd('/')
        if (baseUrl.isBlank()) return@withContext null

        try {
            val url = buildString {
                append("$baseUrl/api/competitions/$date/runners")
                if (changedSinceMs != null) append("?changedSince=${toIso(changedSinceMs)}")
            }

            val request = Request.Builder().url(url).get().build()
            okHttpClient.newCall(request).execute().use { response ->
                if (!response.isSuccessful) return@withContext null
                val json = JSONObject(response.body!!.string())
                val serverTimeUtc = parseIso(json.getString("serverTimeUtc"))
                val runnersArray = json.getJSONArray("runners")
                val runners = (0 until runnersArray.length()).map { i ->
                    val r = runnersArray.getJSONObject(i)
                    RunnerDto(
                        startNumber = r.getInt("startNumber"),
                        siChipNo = r.optString("siChipNo").takeIf { it.isNotEmpty() },
                        name = r.getString("name"),
                        surname = r.getString("surname"),
                        className = r.getString("className"),
                        clubName = r.getString("clubName"),
                        country = r.optString("country").takeIf { it.isNotEmpty() },
                        statusId = r.getInt("statusId"),
                        statusName = r.getString("statusName"),
                        startPlace = r.getInt("startPlace"),
                        lastModifiedUtc = parseIso(r.getString("lastModifiedUtc")),
                        lastModifiedBy = r.getString("lastModifiedBy")
                    )
                }
                GetRunnersResult(serverTimeUtc, runners)
            }
        } catch (_: Exception) {
            null
        }
    }

    private fun parseIso(iso: String): Long =
        Instant.from(isoFormatter.parse(iso)).toEpochMilli()
}
