package com.orienteering.startref.ui.debug

import androidx.lifecycle.ViewModel
import com.orienteering.startref.data.si.SiDebugLog
import com.orienteering.startref.data.si.SiLogEntry
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.StateFlow
import javax.inject.Inject

@HiltViewModel
class DebugViewModel @Inject constructor(
    private val debugLog: SiDebugLog
) : ViewModel() {
    val entries: StateFlow<List<SiLogEntry>> = debugLog.entries

    fun clear() = debugLog.clear()
}
