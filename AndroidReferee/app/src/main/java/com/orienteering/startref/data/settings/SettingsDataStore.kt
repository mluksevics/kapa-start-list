package com.orienteering.startref.data.settings

import android.content.Context
import androidx.datastore.core.DataStore
import androidx.datastore.preferences.core.Preferences
import androidx.datastore.preferences.core.booleanPreferencesKey
import androidx.datastore.preferences.core.edit
import androidx.datastore.preferences.core.floatPreferencesKey
import androidx.datastore.preferences.core.intPreferencesKey
import androidx.datastore.preferences.core.longPreferencesKey
import androidx.datastore.preferences.core.stringPreferencesKey
import androidx.datastore.preferences.preferencesDataStore
import dagger.hilt.android.qualifiers.ApplicationContext
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.map
import java.time.LocalDate
import javax.inject.Inject
import javax.inject.Singleton

private val Context.dataStore: DataStore<Preferences> by preferencesDataStore(name = "app_settings")

@Singleton
class SettingsDataStore @Inject constructor(
    @ApplicationContext private val context: Context
) {
    private object Keys {
        val API_BASE_URL = stringPreferencesKey("api_base_url")
        val API_KEY = stringPreferencesKey("api_key")
        val POLL_INTERVAL_SECONDS = intPreferencesKey("poll_interval_seconds")
        val START_PLACE = intPreferencesKey("start_place")
        val HEADER_TEXT = stringPreferencesKey("header_text")
        val PRESTART_MINUTES = intPreferencesKey("prestart_minutes")
        val LATE_START_MINUTES = intPreferencesKey("late_start_minutes")
        val SOUND_ENABLED = booleanPreferencesKey("sound_enabled")
        val VIBRATION_ENABLED = booleanPreferencesKey("vibration_enabled")
        val ROW_FONT_SIZE = floatPreferencesKey("row_font_size")
        val COMPETITION_DATE = stringPreferencesKey("competition_date")
        val DEVICE_NAME = stringPreferencesKey("device_name")
        val LAST_SERVER_TIME_UTC = longPreferencesKey("last_server_time_utc")
    }

    val settings: Flow<AppSettings> = context.dataStore.data.map { prefs ->
        AppSettings(
            apiBaseUrl = prefs[Keys.API_BASE_URL] ?: AppSettings.DEFAULT.apiBaseUrl,
            apiKey = prefs[Keys.API_KEY] ?: AppSettings.DEFAULT.apiKey,
            pollIntervalSeconds = prefs[Keys.POLL_INTERVAL_SECONDS] ?: AppSettings.DEFAULT.pollIntervalSeconds,
            startPlace = prefs[Keys.START_PLACE] ?: AppSettings.DEFAULT.startPlace,
            headerText = prefs[Keys.HEADER_TEXT] ?: AppSettings.DEFAULT.headerText,
            prestartMinutes = prefs[Keys.PRESTART_MINUTES] ?: AppSettings.DEFAULT.prestartMinutes,
            lateStartMinutes = prefs[Keys.LATE_START_MINUTES] ?: AppSettings.DEFAULT.lateStartMinutes,
            soundEnabled = prefs[Keys.SOUND_ENABLED] ?: AppSettings.DEFAULT.soundEnabled,
            vibrationEnabled = prefs[Keys.VIBRATION_ENABLED] ?: AppSettings.DEFAULT.vibrationEnabled,
            rowFontSize = prefs[Keys.ROW_FONT_SIZE] ?: AppSettings.DEFAULT_FONT_SIZE,
            competitionDate = prefs[Keys.COMPETITION_DATE] ?: LocalDate.now().toString(),
            deviceName = prefs[Keys.DEVICE_NAME] ?: AppSettings.DEFAULT.deviceName,
            lastServerTimeUtc = prefs[Keys.LAST_SERVER_TIME_UTC] ?: 0L
        )
    }

    suspend fun updateApiBaseUrl(value: String) = context.dataStore.edit { it[Keys.API_BASE_URL] = value }
    suspend fun updateApiKey(value: String) = context.dataStore.edit { it[Keys.API_KEY] = value }
    suspend fun updatePollIntervalSeconds(value: Int) = context.dataStore.edit { it[Keys.POLL_INTERVAL_SECONDS] = value }
    suspend fun updateStartPlace(value: Int) = context.dataStore.edit { it[Keys.START_PLACE] = value }
    suspend fun updateHeaderText(value: String) = context.dataStore.edit { it[Keys.HEADER_TEXT] = value }
    suspend fun updatePrestartMinutes(value: Int) = context.dataStore.edit { it[Keys.PRESTART_MINUTES] = value }
    suspend fun updateLateStartMinutes(value: Int) = context.dataStore.edit { it[Keys.LATE_START_MINUTES] = value }
    suspend fun updateSoundEnabled(value: Boolean) = context.dataStore.edit { it[Keys.SOUND_ENABLED] = value }
    suspend fun updateVibrationEnabled(value: Boolean) = context.dataStore.edit { it[Keys.VIBRATION_ENABLED] = value }
    suspend fun updateRowFontSize(value: Float) = context.dataStore.edit { it[Keys.ROW_FONT_SIZE] = value }
    suspend fun updateCompetitionDate(value: String) = context.dataStore.edit { it[Keys.COMPETITION_DATE] = value }
    suspend fun updateDeviceName(value: String) = context.dataStore.edit { it[Keys.DEVICE_NAME] = value }
    suspend fun updateLastServerTimeUtc(value: Long) = context.dataStore.edit { it[Keys.LAST_SERVER_TIME_UTC] = value }
}
