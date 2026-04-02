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

    fun log(message: String) {
        val entry = SiLogEntry(LocalTime.now().format(formatter), message)
        _entries.value = (_entries.value + entry).takeLast(200)
        Log.d("SiReader", message)
    }

    fun clear() {
        _entries.value = emptyList()
    }
}
