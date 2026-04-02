package com.orienteering.startref.ui.settings

import android.content.Context
import android.hardware.usb.UsbManager
import android.util.Log
import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import androidx.work.ExistingWorkPolicy
import androidx.work.OneTimeWorkRequestBuilder
import androidx.work.WorkManager
import com.orienteering.startref.data.si.SiProber
import com.orienteering.startref.data.repository.StartListRepository
import com.orienteering.startref.data.settings.AppSettings
import com.orienteering.startref.data.settings.SettingsDataStore
import com.orienteering.startref.data.sync.PendingSyncWorker
import dagger.hilt.android.lifecycle.HiltViewModel
import dagger.hilt.android.qualifiers.ApplicationContext
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
    private val workManager: WorkManager,
    @ApplicationContext private val context: Context
) : ViewModel() {

    val settings: StateFlow<AppSettings> = settingsDataStore.settings
        .stateIn(viewModelScope, SharingStarted.Eagerly, AppSettings.DEFAULT)

    private val _isLoading = MutableStateFlow(false)
    val isLoading: StateFlow<Boolean> = _isLoading.asStateFlow()

    private val _message = MutableStateFlow<String?>(null)
    val message: StateFlow<String?> = _message.asStateFlow()

    // List of (key, displayName) pairs for connected USB serial devices.
    // Refreshed by calling refreshSerialDevices().
    private val _availableSerialDevices = MutableStateFlow<List<Pair<String, String>>>(emptyList())
    val availableSerialDevices: StateFlow<List<Pair<String, String>>> = _availableSerialDevices.asStateFlow()

    fun refreshSerialDevices() {
        val usbManager = context.getSystemService(Context.USB_SERVICE) as UsbManager

        // Log all raw USB devices so you can see VID:PID in Logcat
        usbManager.deviceList.values.forEach { device ->
            Log.d("SiReader", "USB device: name=${device.deviceName} " +
                "VID=${device.vendorId} (0x${device.vendorId.toString(16).uppercase()}) " +
                "PID=${device.productId} (0x${device.productId.toString(16).uppercase()}) " +
                "class=${device.deviceClass}")
        }

        val drivers = SiProber.get().findAllDrivers(usbManager)
        Log.d("SiReader", "UsbSerialProber found ${drivers.size} driver(s)")
        drivers.forEach { d ->
            Log.d("SiReader", "  driver: ${d.javaClass.simpleName} VID=${d.device.vendorId} PID=${d.device.productId}")
        }

        _availableSerialDevices.value = drivers.map { driver ->
            val key = "${driver.device.vendorId}:${driver.device.productId}"
            val name = driver.device.deviceName ?: key
            key to "$key — $name"
        }
    }

    fun clearMessage() { _message.value = null }

    /** Suspends until the real stored settings are available (not DEFAULT). */
    suspend fun awaitSettings(): AppSettings = settingsDataStore.settings.first()

    fun updateApiBaseUrl(value: String) = viewModelScope.launch { settingsDataStore.updateApiBaseUrl(value) }
    fun updateApiKey(value: String) = viewModelScope.launch { settingsDataStore.updateApiKey(value) }
    fun updatePollIntervalSeconds(value: Int) = viewModelScope.launch { settingsDataStore.updatePollIntervalSeconds(value) }
    fun updateStartPlace(value: Int) = viewModelScope.launch { settingsDataStore.updateStartPlace(value) }
    fun updateHeaderText(value: String) = viewModelScope.launch { settingsDataStore.updateHeaderText(value) }
    fun updatePrestartMinutes(value: Int) = viewModelScope.launch { settingsDataStore.updatePrestartMinutes(value) }
    fun updateLateStartMinutes(value: Int) = viewModelScope.launch { settingsDataStore.updateLateStartMinutes(value) }
    fun updateSoundEnabled(value: Boolean) = viewModelScope.launch { settingsDataStore.updateSoundEnabled(value) }
    fun updateVibrationEnabled(value: Boolean) = viewModelScope.launch { settingsDataStore.updateVibrationEnabled(value) }
    fun updateRowFontSize(value: Float) = viewModelScope.launch { settingsDataStore.updateRowFontSize(value) }
    fun updateDeviceName(value: String) = viewModelScope.launch {
        val trimmed = value.trim()
        if (trimmed.isNotEmpty()) settingsDataStore.updateDeviceName(trimmed)
    }

    fun updateSiReaderDeviceKey(key: String) = viewModelScope.launch {
        settingsDataStore.updateSiReaderDeviceKey(key)
    }

    fun reloadStartList() {
        viewModelScope.launch {
            _isLoading.value = true
            try {
                repository.reloadFromApi()
                _message.value = "Start list loaded successfully"
            } catch (e: Exception) {
                _message.value = "Failed to load: ${e.message}"
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

    fun pullClasses() {
        viewModelScope.launch {
            _isLoading.value = true
            try {
                repository.pullClasses()
                _message.value = "Classes pulled"
            } catch (e: Exception) {
                _message.value = "Classes pull failed: ${e.message}"
            } finally {
                _isLoading.value = false
            }
        }
    }

    fun pullClubs() {
        viewModelScope.launch {
            _isLoading.value = true
            try {
                repository.pullClubs()
                _message.value = "Clubs pulled"
            } catch (e: Exception) {
                _message.value = "Clubs pull failed: ${e.message}"
            } finally {
                _isLoading.value = false
            }
        }
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
