package com.orienteering.startref.ui.startlist

import android.content.Context
import android.media.AudioManager
import android.media.ToneGenerator
import android.os.Build
import android.os.VibrationEffect
import android.os.Vibrator
import android.os.VibratorManager
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import androidx.work.ExistingWorkPolicy
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.WorkManager
import com.orienteering.startref.data.local.ClassEntry
import com.orienteering.startref.data.local.ClubEntry
import com.orienteering.startref.data.local.entity.RunnerEntity
import com.orienteering.startref.data.repository.StartListRepository
import com.orienteering.startref.data.settings.AppSettings
import com.orienteering.startref.data.settings.SettingsDataStore
import com.orienteering.startref.data.sync.PendingSyncWorker
import com.orienteering.startref.data.sync.SyncManager
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.collect
import kotlinx.coroutines.flow.combine
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import javax.inject.Inject

sealed interface StartListItem {
    data class Header(val timeMinute: Long, val isCurrent: Boolean) : StartListItem
    data class Row(val runner: RunnerEntity) : StartListItem
}

@HiltViewModel
class StartListViewModel @Inject constructor(
    private val repository: StartListRepository,
    private val settingsDataStore: SettingsDataStore,
    private val syncManager: SyncManager,
    private val workManager: WorkManager,
    @ApplicationContext private val context: Context
) : ViewModel() {

    private val _currentTimeMs = MutableStateFlow(System.currentTimeMillis())
    val currentTimeMs: StateFlow<Long> = _currentTimeMs.asStateFlow()

    private val _selectedRunner = MutableStateFlow<RunnerEntity?>(null)
    val selectedRunner: StateFlow<RunnerEntity?> = _selectedRunner.asStateFlow()

    private val _isLoading = MutableStateFlow(false)
    val isLoading: StateFlow<Boolean> = _isLoading.asStateFlow()

    private val _message = MutableStateFlow<String?>(null)
    val message: StateFlow<String?> = _message.asStateFlow()

    private val _autoScrollEnabled = MutableStateFlow(false)
    val autoScrollEnabled: StateFlow<Boolean> = _autoScrollEnabled.asStateFlow()

    val settings: StateFlow<AppSettings> = settingsDataStore.settings
        .stateIn(viewModelScope, SharingStarted.Eagerly, AppSettings.DEFAULT)

    val availableClasses: StateFlow<List<ClassEntry>> = combine(
        repository.observeLookupClasses(),
        repository.observeClasses()
    ) { lookupClasses, runnerClasses ->
        if (lookupClasses.isNotEmpty()) lookupClasses else runnerClasses
    }.stateIn(viewModelScope, SharingStarted.Eagerly, emptyList())

    val availableClubs: StateFlow<List<ClubEntry>> = repository.observeLookupClubs()
        .stateIn(viewModelScope, SharingStarted.Eagerly, emptyList())

    val syncCounts: StateFlow<Pair<Int, Int>> = repository.observeSyncCounts()
        .stateIn(viewModelScope, SharingStarted.Eagerly, 0 to 0)

    val isSyncing: StateFlow<Boolean> = syncManager.isSyncing
        .stateIn(viewModelScope, SharingStarted.Eagerly, false)

    private val currentTimeMinute = _currentTimeMs
        .map { it / 60_000L }
        .stateIn(viewModelScope, SharingStarted.Eagerly, System.currentTimeMillis() / 60_000L)

    val startListItems: StateFlow<List<StartListItem>> = combine(
        repository.observeRunners(),
        currentTimeMinute,
        settings
    ) { runners, timeMinute, appSettings ->
        val s = appSettings
        val adjustedMinute = timeMinute - s.lateStartMinutes - s.prestartMinutes
        val tod = ((adjustedMinute.toInt() % (24 * 60)) + (24 * 60)) % (24 * 60)
        val filtered = if (s.startPlace == 0) runners else runners.filter { it.startPlace == s.startPlace }
        buildItems(filtered, tod)
    }.stateIn(viewModelScope, SharingStarted.Eagerly, emptyList())

    private var lastAlertMinute = -1L

    init {
        viewModelScope.launch {
            while (true) {
                val now = System.currentTimeMillis()
                _currentTimeMs.value = now

                val currentMinute = now / 60_000
                if (currentMinute != lastAlertMinute) {
                    if (lastAlertMinute != -1L) triggerMinuteAlert()
                    lastAlertMinute = currentMinute
                }

                delay(1000 - (now % 1000))
            }
        }

        viewModelScope.launch {
            syncManager.syncDeltas.collect { delta ->
                if (delta.classNamesChanged > 0 || delta.clubNamesChanged > 0) {
                    _message.value = "Changes synced: classes ${delta.classNamesChanged}, clubs ${delta.clubNamesChanged}"
                }
            }
        }
    }

    fun toggleAutoScroll() { _autoScrollEnabled.value = !_autoScrollEnabled.value }

    fun selectRunner(runner: RunnerEntity?) { _selectedRunner.value = runner }

    fun toggleStarted(startNumber: Int) {
        viewModelScope.launch { repository.toggleStarted(startNumber) }
    }

    fun toggleDns(startNumber: Int) {
        viewModelScope.launch { repository.toggleDns(startNumber) }
    }

    fun updateRunner(runner: RunnerEntity) {
        viewModelScope.launch {
            repository.updateRunner(runner)
            _selectedRunner.value = null
        }
    }

    fun reloadStartList() {
        viewModelScope.launch {
            _isLoading.value = true
            try {
                repository.reloadFromApi()
                _message.value = "Start list loaded"
            } catch (e: Exception) {
                _message.value = "Load failed: ${e.message}"
            } finally {
                _isLoading.value = false
            }
        }
    }

    fun forcePush() {
        val request = OneTimeWorkRequestBuilder<PendingSyncWorker>().build()
        workManager.enqueueUniqueWork("forcePush", ExistingWorkPolicy.REPLACE, request)
        _message.value = "Pushing pending updates..."
    }

    fun clearMessage() { _message.value = null }

    fun currentHeaderIndex(): Int {
        val tod = highlightedTimeOfDay()
        return startListItems.value.indexOfFirst {
            it is StartListItem.Header && it.timeMinute.minuteOfDay() == tod
        }.coerceAtLeast(0)
    }

    private fun buildItems(runners: List<RunnerEntity>, highlightedTod: Int): List<StartListItem> = buildList {
        runners.groupBy { it.startTime.minuteBoundary() }
            .entries.sortedBy { it.key }
            .forEach { (minute, group) ->
                add(StartListItem.Header(minute, minute.minuteOfDay() == highlightedTod))
                group.sortedBy { it.startNumber }.forEach { add(StartListItem.Row(it)) }
            }
    }

    fun highlightedTimeOfDay(timeMs: Long = _currentTimeMs.value): Int {
        val s = settings.value
        val adjustedMs = timeMs - (s.lateStartMinutes * 60_000L) - (s.prestartMinutes * 60_000L)
        return ((adjustedMs / 60_000).toInt() % (24 * 60) + 24 * 60) % (24 * 60)
    }

    private fun Long.minuteBoundary(): Long = (this / 60_000) * 60_000

    private fun Long.minuteOfDay(): Int = ((this / 60_000).toInt() % (24 * 60) + 24 * 60) % (24 * 60)

    private fun triggerMinuteAlert() {
        val s = settings.value
        if (s.soundEnabled) {
            try {
                ToneGenerator(AudioManager.STREAM_NOTIFICATION, 80)
                    .startTone(ToneGenerator.TONE_PROP_BEEP, 300)
            } catch (_: Exception) {}
        }
        if (s.vibrationEnabled) {
            try {
                if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
                    val vm = context.getSystemService(Context.VIBRATOR_MANAGER_SERVICE) as VibratorManager
                    vm.defaultVibrator.vibrate(VibrationEffect.createOneShot(300, VibrationEffect.DEFAULT_AMPLITUDE))
                } else {
                    @Suppress("DEPRECATION")
                    val vibrator = context.getSystemService(Context.VIBRATOR_SERVICE) as Vibrator
                    vibrator.vibrate(VibrationEffect.createOneShot(300, VibrationEffect.DEFAULT_AMPLITUDE))
                }
            } catch (_: Exception) {}
        }
    }
}
