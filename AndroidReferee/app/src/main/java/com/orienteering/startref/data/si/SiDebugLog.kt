package com.orienteering.startref.data.si

import android.util.Log
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import java.time.LocalTime
import java.time.format.DateTimeFormatter
import javax.inject.Inject
import javax.inject.Singleton

data class SiLogEntry(val time: String, val message: String)

@Singleton
class SiDebugLog @Inject constructor() {
    private val _entries = MutableStateFlow<List<SiLogEntry>>(emptyList())
    val entries: StateFlow<List<SiLogEntry>> = _entries.asStateFlow()

    private val formatter = DateTimeFormatter.ofPattern("HH:mm:ss.SSS")

    // Bounded ring buffer — one snapshot allocation per log call instead of two.
    private val ring = ArrayDeque<SiLogEntry>(MAX_ENTRIES)

    @Synchronized
    fun log(message: String) {
        ring.addLast(SiLogEntry(LocalTime.now().format(formatter), message))
        while (ring.size > MAX_ENTRIES) ring.removeFirst()
        _entries.value = ring.toList()
        Log.d("SiReader", message)
    }

    @Synchronized
    fun clear() {
        ring.clear()
        _entries.value = emptyList()
    }

    private companion object {
        const val MAX_ENTRIES = 200
    }
}
