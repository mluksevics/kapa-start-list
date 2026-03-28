package com.orienteering.startref.data.settings

import java.time.LocalDate

data class AppSettings(
    val apiBaseUrl: String,
    val apiKey: String,
    val pollIntervalSeconds: Int,
    val startPlace: Int,
    val headerText: String,
    val prestartMinutes: Int,
    val lateStartMinutes: Int,
    val soundEnabled: Boolean,
    val vibrationEnabled: Boolean,
    val rowFontSize: Float,
    val competitionDate: String,      // ISO date yyyy-MM-dd, defaults to today
    val deviceName: String,           // identifies this device in lastModifiedBy
    val lastServerTimeUtc: Long       // watermark for delta sync polling
) {
    companion object {
        const val DEFAULT_FONT_SIZE = 16f
        val DEFAULT = AppSettings(
            apiBaseUrl = "https://startref.azurewebsites.net/",
            apiKey = "marcisTestKey",
            pollIntervalSeconds = 30,
            startPlace = 0,
            headerText = "Orienteering Start",
            prestartMinutes = -2,
            lateStartMinutes = 0,
            soundEnabled = false,
            vibrationEnabled = false,
            rowFontSize = DEFAULT_FONT_SIZE,
            competitionDate = LocalDate.now().toString(),
            deviceName = "android-referee",
            lastServerTimeUtc = 0L
        )
    }
}
