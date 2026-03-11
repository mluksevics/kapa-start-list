package com.orienteering.startref.data.remote

import android.util.Base64
import com.orienteering.startref.data.settings.AppSettings
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import java.net.URLEncoder
import javax.crypto.Mac
import javax.crypto.spec.SecretKeySpec
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class AzureServiceBusClient @Inject constructor(
    private val okHttpClient: OkHttpClient
) {
    suspend fun sendMessage(payload: String, settings: AppSettings): Boolean = withContext(Dispatchers.IO) {
        try {
            val parts = parseConnectionString(settings.serviceBusConnectionString)
                ?: return@withContext false
            val queueName = parts.entityPath ?: settings.serviceBusQueueName
            val resourceUri = "https://${parts.namespace}.servicebus.windows.net/$queueName"
            val sasToken = generateSasToken(resourceUri, parts.keyName, parts.key)

            val request = Request.Builder()
                .url("$resourceUri/messages")
                .addHeader("Authorization", sasToken)
                .post(payload.toRequestBody("application/json".toMediaType()))
                .build()

            okHttpClient.newCall(request).execute().use { response ->
                response.isSuccessful
            }
        } catch (e: Exception) {
            false
        }
    }

    private fun parseConnectionString(cs: String): ConnectionStringParts? {
        return try {
            val map = cs.split(";").mapNotNull { part ->
                val idx = part.indexOf('=')
                if (idx > 0) part.substring(0, idx).trim() to part.substring(idx + 1).trim()
                else null
            }.toMap()

            val endpoint = map["Endpoint"] ?: return null
            val namespace = endpoint
                .removePrefix("sb://")
                .removeSuffix(".servicebus.windows.net/")
                .trimEnd('/')

            ConnectionStringParts(
                namespace = namespace,
                keyName = map["SharedAccessKeyName"] ?: return null,
                key = map["SharedAccessKey"] ?: return null,
                entityPath = map["EntityPath"]
            )
        } catch (e: Exception) {
            null
        }
    }

    private fun generateSasToken(resourceUri: String, keyName: String, key: String): String {
        val expiry = (System.currentTimeMillis() / 1000) + 3600
        val encodedUri = URLEncoder.encode(resourceUri, "UTF-8")
        val stringToSign = "$encodedUri\n$expiry"

        val mac = Mac.getInstance("HmacSHA256")
        mac.init(SecretKeySpec(key.toByteArray(Charsets.UTF_8), "HmacSHA256"))
        val signature = Base64.encodeToString(
            mac.doFinal(stringToSign.toByteArray(Charsets.UTF_8)),
            Base64.NO_WRAP
        )
        val encodedSig = URLEncoder.encode(signature, "UTF-8")

        return "SharedAccessSignature sr=$encodedUri&sig=$encodedSig&se=$expiry&skn=$keyName"
    }

    private data class ConnectionStringParts(
        val namespace: String,
        val keyName: String,
        val key: String,
        val entityPath: String?
    )
}
