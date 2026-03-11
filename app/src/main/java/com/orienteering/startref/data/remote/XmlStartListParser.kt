package com.orienteering.startref.data.remote

import android.content.Context
import android.util.Xml
import com.orienteering.startref.data.local.entity.RunnerEntity
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.withContext
import okhttp3.OkHttpClient
import okhttp3.Request
import org.xmlpull.v1.XmlPullParser
import java.io.InputStream
import java.time.LocalDateTime
import java.time.OffsetDateTime
import java.time.ZoneId
import java.time.format.DateTimeFormatter
import java.time.format.DateTimeFormatterBuilder
import java.time.temporal.ChronoField
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class XmlStartListParser @Inject constructor(
    private val okHttpClient: OkHttpClient
) {
    private val localDateTimeFormatter = DateTimeFormatterBuilder()
        .appendPattern("yyyy-MM-dd'T'HH:mm:ss")
        .optionalStart().appendFraction(ChronoField.NANO_OF_SECOND, 1, 9, true).optionalEnd()
        .toFormatter()

    suspend fun fetchAndParse(url: String): List<RunnerEntity> = withContext(Dispatchers.IO) {
        val request = Request.Builder().url(url).build()
        okHttpClient.newCall(request).execute().use { response ->
            val stream = response.body?.byteStream()
                ?: throw IllegalStateException("Empty response from $url")
            parseStream(stream)
        }
    }

    suspend fun parseFromAsset(context: Context, assetName: String): List<RunnerEntity> =
        withContext(Dispatchers.IO) {
            context.assets.open(assetName).use { parseStream(it) }
        }

    private fun parseStream(inputStream: InputStream): List<RunnerEntity> {
        val parser = Xml.newPullParser()
        parser.setFeature(XmlPullParser.FEATURE_PROCESS_NAMESPACES, false)
        // null = let the parser read encoding from the XML declaration (handles windows-1257)
        parser.setInput(inputStream, null)
        return doParse(parser)
    }

    private fun doParse(parser: XmlPullParser): List<RunnerEntity> {
        val runners = mutableListOf<RunnerEntity>()
        val tagStack = ArrayDeque<String>()

        var currentClassName = ""
        var startNumber = 0
        var name = ""
        var surname = ""
        var siCard = ""
        var clubName = ""
        var startTimeStr = ""

        var inPersonStart = false
        var inPerson = false
        var inPersonName = false
        var inOrganisation = false
        var inStart = false
        var inClassDirect = false  // true only when directly inside <ClassStart><Class>

        var event = parser.eventType
        while (event != XmlPullParser.END_DOCUMENT) {
            when (event) {
                XmlPullParser.START_TAG -> {
                    val tag = parser.name
                    tagStack.addLast(tag)
                    val parent = tagStack.getOrNull(tagStack.size - 2)

                    when {
                        tag == "ClassStart" -> currentClassName = ""
                        // Only track Class that is a direct child of ClassStart (not nested)
                        tag == "Class" && parent == "ClassStart" -> inClassDirect = true
                        tag == "PersonStart" -> {
                            inPersonStart = true
                            startNumber = 0
                            name = ""
                            surname = ""
                            siCard = ""
                            clubName = ""
                            startTimeStr = ""
                        }
                        inPersonStart && tag == "Person" -> inPerson = true
                        inPerson && tag == "Name" -> inPersonName = true
                        inPersonStart && !inPerson && !inStart && tag == "Organisation" -> inOrganisation = true
                        inPersonStart && tag == "Start" -> inStart = true
                    }
                }

                XmlPullParser.TEXT -> {
                    val text = parser.text?.trim() ?: ""
                    if (text.isEmpty()) {
                        event = parser.next()
                        continue
                    }
                    val currentTag = tagStack.lastOrNull() ?: ""
                    val parentTag = tagStack.getOrNull(tagStack.size - 2) ?: ""

                    when {
                        // Only read class name when <Name> is a direct child of <Class>
                        inClassDirect && currentTag == "Name" && parentTag == "Class" ->
                            currentClassName = text

                        inPersonName && currentTag == "Family" -> surname = text
                        inPersonName && currentTag == "Given" -> name = text
                        inOrganisation && currentTag == "Name" && parentTag == "Organisation" ->
                            clubName = text

                        inStart && currentTag == "BibNumber" ->
                            startNumber = text.toIntOrNull() ?: startNumber

                        inStart && currentTag == "StartPosition" && startNumber == 0 ->
                            startNumber = text.toIntOrNull() ?: 0

                        inStart && currentTag == "StartTime" && parentTag == "Start" ->
                            startTimeStr = text

                        inStart && currentTag == "ControlCard" -> siCard = text
                    }
                }

                XmlPullParser.END_TAG -> {
                    val tag = parser.name
                    tagStack.removeLastOrNull()
                    when {
                        tag == "PersonStart" -> {
                            if (startNumber > 0 && startTimeStr.isNotEmpty()) {
                                runners.add(
                                    RunnerEntity(
                                        startNumber = startNumber,
                                        name = name,
                                        surname = surname,
                                        siCard = siCard,
                                        className = currentClassName,
                                        clubName = clubName,
                                        startTime = parseTime(startTimeStr)
                                    )
                                )
                            }
                            inPersonStart = false
                            inPerson = false
                            inPersonName = false
                            inOrganisation = false
                            inStart = false
                        }
                        tag == "Class" -> inClassDirect = false
                        tag == "Person" -> inPerson = false
                        tag == "Name" && inPerson -> inPersonName = false
                        tag == "Organisation" -> inOrganisation = false
                        tag == "Start" -> inStart = false
                    }
                }
            }
            event = parser.next()
        }
        return runners
    }

    private fun parseTime(timeStr: String): Long {
        return try {
            OffsetDateTime.parse(timeStr).toInstant().toEpochMilli()
        } catch (_: Exception) {
            try {
                LocalDateTime.parse(timeStr, localDateTimeFormatter)
                    .atZone(ZoneId.systemDefault())
                    .toInstant()
                    .toEpochMilli()
            } catch (_: Exception) {
                0L
            }
        }
    }
}
