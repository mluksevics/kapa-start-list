package com.orienteering.startref.ui.settings

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import androidx.work.ExistingWorkPolicy
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.WorkManager
import com.orienteering.startref.data.repository.StartListRepository
import com.orienteering.startref.data.settings.AppSettings
import com.orienteering.startref.data.settings.SettingsDataStore
import com.orienteering.startref.data.sync.PendingSyncWorker
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharingStarted
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.flow.stateIn
import kotlinx.coroutines.launch
import javax.inject.Inject

@HiltViewModel
class SettingsViewModel @Inject constructor(
    private val settingsDataStore: SettingsDataStore,
    private val repository: StartListRepository,
    private val workManager: WorkManager
) : ViewModel() {

    val settings: StateFlow<AppSettings> = settingsDataStore.settings
        .stateIn(viewModelScope, SharingStarted.Eagerly, AppSettings.DEFAULT)

    private val _isLoading = MutableStateFlow(false)
    val isLoading: StateFlow<Boolean> = _isLoading.asStateFlow()

    private val _message = MutableStateFlow<String?>(null)
    val message: StateFlow<String?> = _message.asStateFlow()

    fun clearMessage() { _message.value = null }

    /** Suspends until the real stored settings are available (not DEFAULT). */
    suspend fun awaitSettings(): AppSettings = settingsDataStore.settings.first()

    fun updateXmlUrl(value: String) = viewModelScope.launch { settingsDataStore.updateXmlUrl(value) }
    fun updateServiceBusCs(value: String) = viewModelScope.launch { settingsDataStore.updateServiceBusCs(value) }
    fun updateServiceBusQueue(value: String) = viewModelScope.launch { settingsDataStore.updateServiceBusQueue(value) }
    fun updateHeaderText(value: String) = viewModelScope.launch { settingsDataStore.updateHeaderText(value) }
    fun updatePrestartMinutes(value: Int) = viewModelScope.launch { settingsDataStore.updatePrestartMinutes(value) }
    fun updateLateStartMinutes(value: Int) = viewModelScope.launch { settingsDataStore.updateLateStartMinutes(value) }
    fun updateSoundEnabled(value: Boolean) = viewModelScope.launch { settingsDataStore.updateSoundEnabled(value) }
    fun updateVibrationEnabled(value: Boolean) = viewModelScope.launch { settingsDataStore.updateVibrationEnabled(value) }
    fun updateRowFontSize(value: Float) = viewModelScope.launch { settingsDataStore.updateRowFontSize(value) }

    fun reloadStartList() {
        viewModelScope.launch {
            _isLoading.value = true
            try {
                repository.reloadFromXml(settings.value.xmlUrl)
                _message.value = "Start list loaded successfully"
            } catch (e: Exception) {
                _message.value = "Failed to load: ${e.message}"
            } finally {
                _isLoading.value = false
            }
        }
    }

    fun loadSampleData() {
        viewModelScope.launch {
            _isLoading.value = true
            try {
                repository.loadFromAsset("startlis.xml")
                _message.value = "Sample data loaded (startlis.xml)"
            } catch (e: Exception) {
                _message.value = "Failed to load sample: ${e.message}"
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

    fun exportToCsv() {
        viewModelScope.launch {
            try {
                repository.exportToCsv()
                _message.value = "Exported to Downloads folder"
            } catch (e: Exception) {
                _message.value = "Export failed: ${e.message}"
            }
        }
    }

    fun clearCache() {
        viewModelScope.launch {
            repository.clearAllData()
            _message.value = "Cache cleared"
        }
    }
}
