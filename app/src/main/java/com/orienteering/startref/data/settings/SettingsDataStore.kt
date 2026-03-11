package com.orienteering.startref.data.settings

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.floatPreferencesKey
import androidx.datastore.preferences.core.intPreferencesKey
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import javax.inject.Inject
import javax.inject.Singleton

private val Context.dataStore: DataStore<Preferences> by preferencesDataStore(name = "app_settings")

@Singleton
class SettingsDataStore @Inject constructor(
    @ApplicationContext private val context: Context
) {
    private object Keys {
        val XML_URL = stringPreferencesKey("xml_url")
        val SERVICE_BUS_CS = stringPreferencesKey("service_bus_cs")
        val SERVICE_BUS_QUEUE = stringPreferencesKey("service_bus_queue")
        val HEADER_TEXT = stringPreferencesKey("header_text")
        val PRESTART_MINUTES = intPreferencesKey("prestart_minutes")
        val LATE_START_MINUTES = intPreferencesKey("late_start_minutes")
        val SOUND_ENABLED = booleanPreferencesKey("sound_enabled")
        val VIBRATION_ENABLED = booleanPreferencesKey("vibration_enabled")
        val ROW_FONT_SIZE = floatPreferencesKey("row_font_size")
    }

    val settings: Flow<AppSettings> = context.dataStore.data.map { prefs ->
        AppSettings(
            xmlUrl = prefs[Keys.XML_URL] ?: AppSettings.DEFAULT.xmlUrl,
            serviceBusConnectionString = prefs[Keys.SERVICE_BUS_CS] ?: "",
            serviceBusQueueName = prefs[Keys.SERVICE_BUS_QUEUE] ?: AppSettings.DEFAULT.serviceBusQueueName,
            headerText = prefs[Keys.HEADER_TEXT] ?: AppSettings.DEFAULT.headerText,
            prestartMinutes = prefs[Keys.PRESTART_MINUTES] ?: AppSettings.DEFAULT.prestartMinutes,
            lateStartMinutes = prefs[Keys.LATE_START_MINUTES] ?: AppSettings.DEFAULT.lateStartMinutes,
            soundEnabled = prefs[Keys.SOUND_ENABLED] ?: AppSettings.DEFAULT.soundEnabled,
            vibrationEnabled = prefs[Keys.VIBRATION_ENABLED] ?: AppSettings.DEFAULT.vibrationEnabled,
            rowFontSize = prefs[Keys.ROW_FONT_SIZE] ?: AppSettings.DEFAULT_FONT_SIZE
        )
    }

    suspend fun updateXmlUrl(value: String) = context.dataStore.edit { it[Keys.XML_URL] = value }
    suspend fun updateServiceBusCs(value: String) = context.dataStore.edit { it[Keys.SERVICE_BUS_CS] = value }
    suspend fun updateServiceBusQueue(value: String) = context.dataStore.edit { it[Keys.SERVICE_BUS_QUEUE] = value }
    suspend fun updateHeaderText(value: String) = context.dataStore.edit { it[Keys.HEADER_TEXT] = value }
    suspend fun updatePrestartMinutes(value: Int) = context.dataStore.edit { it[Keys.PRESTART_MINUTES] = value }
    suspend fun updateLateStartMinutes(value: Int) = context.dataStore.edit { it[Keys.LATE_START_MINUTES] = value }
    suspend fun updateSoundEnabled(value: Boolean) = context.dataStore.edit { it[Keys.SOUND_ENABLED] = value }
    suspend fun updateVibrationEnabled(value: Boolean) = context.dataStore.edit { it[Keys.VIBRATION_ENABLED] = value }
    suspend fun updateRowFontSize(value: Float) = context.dataStore.edit { it[Keys.ROW_FONT_SIZE] = value }
}
