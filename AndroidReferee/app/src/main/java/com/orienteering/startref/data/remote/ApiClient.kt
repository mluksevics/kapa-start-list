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
    val classId: Int,
    val className: String,
    val clubId: Int,
    val clubName: String,
    val country: String?,
    val statusId: Int,
    val statusName: String,
    val startPlace: Int,
    val startTime: String?,
    val lastModifiedUtc: Long,
    val lastModifiedBy: String
)

data class GetRunnersResult(
    val serverTimeUtc: Long,
    val runners: List<RunnerDto>
)

data class LookupItemDto(
    val id: Int,
    val name: String
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
        classId: Int?,
        clubId: Int?,
        country: String?,
        startPlace: Int?,
        startTime: String?,
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
                classId?.let { put("classId", it) }
                clubId?.let { put("clubId", it) }
                country?.let { put("country", it) }
                startPlace?.let { put("startPlace", it) }
                startTime?.let { put("startTime", it) }
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
                        classId = r.optInt("classId", 0),
                        className = r.getString("className"),
                        clubId = r.optInt("clubId", 0),
                        clubName = r.getString("clubName"),
                        country = r.optString("country").takeIf { it.isNotEmpty() },
                        statusId = r.getInt("statusId"),
                        statusName = r.getString("statusName"),
                        startPlace = r.getInt("startPlace"),
                        startTime = r.optString("startTime").takeIf { it.isNotEmpty() },
                        lastModifiedUtc = parseIso(r.getString("lastModifiedUtc")),
                        lastModifiedBy = r.getString("lastModifiedBy")
                    )
                }
                GetRunnersResult(serverTimeUtc, runners)
            }
        } catch (e: Exception) {
            throw IllegalStateException("getRunners failed: ${e.javaClass.simpleName}: ${e.message}", e)
        }
    }

    suspend fun getClasses(settings: AppSettings): List<LookupItemDto> = withContext(Dispatchers.IO) {
        val baseUrl = settings.apiBaseUrl.trimEnd('/')
        val apiKey = settings.apiKey
        if (baseUrl.isBlank() || apiKey.isBlank()) return@withContext emptyList()

        try {
            val request = Request.Builder()
                .url("$baseUrl/api/lookups/classes")
                .addHeader("X-Api-Key", apiKey)
                .get()
                .build()

            okHttpClient.newCall(request).execute().use { response ->
                if (!response.isSuccessful) return@withContext emptyList()
                val array = JSONArray(response.body!!.string())
                (0 until array.length())
                    .map { i -> array.getJSONObject(i) }
                    .map { item ->
                        LookupItemDto(
                            id = item.optInt("id", 0),
                            name = item.optString("name")
                        )
                    }
                    .filter { it.id > 0 && it.name.isNotBlank() }
            }
        } catch (_: Exception) {
            emptyList()
        }
    }

    suspend fun getClubs(settings: AppSettings): List<LookupItemDto> = withContext(Dispatchers.IO) {
        val baseUrl = settings.apiBaseUrl.trimEnd('/')
        val apiKey = settings.apiKey
        if (baseUrl.isBlank() || apiKey.isBlank()) return@withContext emptyList()

        try {
            val request = Request.Builder()
                .url("$baseUrl/api/lookups/clubs")
                .addHeader("X-Api-Key", apiKey)
                .get()
                .build()

            okHttpClient.newCall(request).execute().use { response ->
                if (!response.isSuccessful) return@withContext emptyList()
                val array = JSONArray(response.body!!.string())
                (0 until array.length())
                    .map { i -> array.getJSONObject(i) }
                    .map { item ->
                        LookupItemDto(
                            id = item.optInt("id", 0),
                            name = item.optString("name")
                        )
                    }
                    .filter { it.id > 0 && it.name.isNotBlank() }
            }
        } catch (_: Exception) {
            emptyList()
        }
    }

    private fun parseIso(iso: String): Long =
        Instant.from(isoFormatter.parse(iso)).toEpochMilli()
}
