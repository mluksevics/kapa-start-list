package com.orienteering.startref.ui.gate

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.orienteering.startref.data.local.LookupDao
import com.orienteering.startref.data.local.RunnerDao
import com.orienteering.startref.data.local.entity.RunnerEntity
import com.orienteering.startref.data.repository.StartListRepository
import com.orienteering.startref.data.settings.AppSettings
import com.orienteering.startref.data.settings.SettingsDataStore
import com.orienteering.startref.data.si.SiConnectionState
import com.orienteering.startref.data.si.SiStationReader
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

enum class GateSignal { IDLE, BRIGHT_GREEN, GREEN, ORANGE, RED }

data class GateUiState(
    val currentTimeMs: Long = System.currentTimeMillis(),
    val adjustedCurrentTimeMs: Long = System.currentTimeMillis(),
    val currentMinuteRunners: List<RunnerEntity> = emptyList(),
    val signal: GateSignal = GateSignal.IDLE,
    val lastReadSiCard: String? = null,
    val lastMatchedRunner: RunnerEntity? = null,
    val pendingApproveRunner: RunnerEntity? = null,  // for ORANGE state — runner in wrong minute
    val statusLine: String = "Waiting for SI card...",
    val readerConnected: Boolean = false
) {
    /** True when the current-minute runner rows should be tappable for chip assignment. */
    val rowsClickable get() = signal == GateSignal.RED || signal == GateSignal.ORANGE
}

@HiltViewModel
class GateViewModel @Inject constructor(
    private val runnerDao: RunnerDao,
    private val lookupDao: LookupDao,
    private val repository: StartListRepository,
    settingsDataStore: SettingsDataStore,
    private val siReader: SiStationReader
) : ViewModel() {

    private val _uiState = MutableStateFlow(GateUiState())
    val uiState: StateFlow<GateUiState> = _uiState.asStateFlow()
    private val settings: StateFlow<AppSettings> = settingsDataStore.settings
        .stateIn(viewModelScope, SharingStarted.Eagerly, AppSettings.DEFAULT)

    val canUndo: StateFlow<Boolean> = repository.canUndo
        .stateIn(viewModelScope, SharingStarted.Eagerly, false)
    val canRedo: StateFlow<Boolean> = repository.canRedo
        .stateIn(viewModelScope, SharingStarted.Eagerly, false)

    fun undo() { viewModelScope.launch { repository.undo() } }
    fun redo() { viewModelScope.launch { repository.redo() } }

    init {
        // Clock tick
        viewModelScope.launch {
            while (true) {
                val now = System.currentTimeMillis()
                updateCurrentMinuteRunners(now)
                val s = settings.value
                val adjustedMs = now - (s.prestartMinutes * 60_000L)
                _uiState.update { it.copy(currentTimeMs = now, adjustedCurrentTimeMs = adjustedMs) }
                delay(1000 - (now % 1000))
            }
        }

        // SI card reads
        viewModelScope.launch {
            siReader.cardReadEvents.collect { siCard ->
                handleCardRead(siCard)
            }
        }

        // Connection state
        viewModelScope.launch {
            siReader.connectionState.collect { state ->
                _uiState.update { it.copy(readerConnected = state == SiConnectionState.CONNECTED) }
            }
        }

        // Push device key and loud-sound flag from settings to reader whenever they change
        viewModelScope.launch {
            settings.collect { s ->
                siReader.siReaderDeviceKey = s.siReaderDeviceKey
                siReader.loudSound = s.loudSound
            }
        }
    }

    fun approve() {
        val runner = _uiState.value.pendingApproveRunner ?: return
        viewModelScope.launch {
            repository.markStarted(runner.startNumber)
            _uiState.update { it.copy(
                signal = GateSignal.GREEN,
                pendingApproveRunner = null,
                statusLine = "Approved: #${runner.startNumber} ${runner.name} ${runner.surname}"
            ) }
            delay(2000)
            resetSignal()
        }
    }

    /** Keep chip remembered and rows clickable so referee can tap to assign. */
    fun dismiss() {
        val siCard = _uiState.value.lastReadSiCard ?: return
        _uiState.update { it.copy(
            signal = GateSignal.RED,
            pendingApproveRunner = null,
            statusLine = "Dismissed — tap runner to assign SI $siCard"
        ) }
    }

    /** Called when user taps a runner row (RED or ORANGE state). Assigns chip and marks started. */
    fun assignChipToRunner(runner: RunnerEntity) {
        val siCard = _uiState.value.lastReadSiCard ?: return
        viewModelScope.launch {
            repository.updateRunner(runner.copy(siCard = siCard))
            repository.markStarted(runner.startNumber)
            _uiState.update { it.copy(
                signal = GateSignal.GREEN,
                lastMatchedRunner = runner,
                pendingApproveRunner = null,
                statusLine = "Assigned SI $siCard → #${runner.startNumber} ${runner.name} ${runner.surname}"
            ) }
            delay(2000)
            resetSignal()
        }
    }

    private suspend fun handleCardRead(siCard: String) {
        val now = System.currentTimeMillis()
        val adjustedNow = now - (settings.value.prestartMinutes * 60_000L)
        val currentTod = (adjustedNow / 60_000).toInt() % (24 * 60)
        val allRunners = filterRunnersByStartPlace(runnerDao.getAll())

        // Find runner by SI card
        val matchedRunner = allRunners.firstOrNull { it.siCard == siCard }

        if (matchedRunner == null) {
            // RED — chip not found
            _uiState.update { it.copy(
                signal = GateSignal.RED,
                lastReadSiCard = siCard,
                pendingApproveRunner = null,
                statusLine = "Not found: SI $siCard"
            ) }
            return
        }

        val runnerTod = (matchedRunner.startTime / 60_000).toInt() % (24 * 60)
        val diffMinutes = currentTod - runnerTod

        if (diffMinutes == 0) {
            // BRIGHT_GREEN — exact minute match
            repository.markStarted(matchedRunner.startNumber)
            _uiState.update { it.copy(
                signal = GateSignal.BRIGHT_GREEN,
                lastReadSiCard = siCard,
                lastMatchedRunner = matchedRunner,
                pendingApproveRunner = null,
                statusLine = "SI $siCard → #${matchedRunner.startNumber} ${matchedRunner.name} OK"
            ) }
            delay(2000)
            _uiState.update { it.copy(signal = GateSignal.GREEN) }
            delay(3000)
            resetSignal()
        } else if (diffMinutes in -5..5) {
            // ORANGE — within ±5 min; show details so referee can approve or reassign
            _uiState.update { it.copy(
                signal = GateSignal.ORANGE,
                lastReadSiCard = siCard,
                pendingApproveRunner = matchedRunner,
                statusLine = "Wrong minute: SI $siCard → #${matchedRunner.startNumber} ${matchedRunner.name} ${matchedRunner.surname} [${matchedRunner.className}] (${if (diffMinutes > 0) "+$diffMinutes" else "$diffMinutes"} min)"
            ) }
        } else {
            // RED — found but too far from start time
            _uiState.update { it.copy(
                signal = GateSignal.RED,
                lastReadSiCard = siCard,
                pendingApproveRunner = null,
                statusLine = "Wrong time: SI $siCard → #${matchedRunner.startNumber} ${matchedRunner.name} ${matchedRunner.surname} ($diffMinutes min off)"
            ) }
        }
    }

    private suspend fun updateCurrentMinuteRunners(nowMs: Long) {
        val adjustedMs = nowMs - (settings.value.prestartMinutes * 60_000L)
        val currentTod = (adjustedMs / 60_000).toInt() % (24 * 60)
        val all = filterRunnersByStartPlace(runnerDao.getAll())
        val current = all.filter { r ->
            (r.startTime / 60_000).toInt() % (24 * 60) == currentTod
        }.sortedBy { it.startNumber }
        _uiState.update { it.copy(currentMinuteRunners = current) }
    }

    private fun resetSignal() {
        _uiState.update { it.copy(
            signal = GateSignal.IDLE,
            lastReadSiCard = null,
            lastMatchedRunner = null,
            pendingApproveRunner = null,
            statusLine = "Waiting for SI card..."
        ) }
    }

    private suspend fun filterRunnersByStartPlace(runners: List<RunnerEntity>): List<RunnerEntity> {
        val sp = settings.value.startPlace
        if (sp == 0) return runners
        val map = lookupDao.getAllClasses().associate { it.id to it.startPlace }
        return runners.filter { map[it.classId] == sp }
    }
}
