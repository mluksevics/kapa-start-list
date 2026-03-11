package com.orienteering.startref.data.settings

data class AppSettings(
    val xmlUrl: String,
    val serviceBusConnectionString: String,
    val serviceBusQueueName: String,
    val headerText: String,
    val prestartMinutes: Int,
    val lateStartMinutes: Int,
    val soundEnabled: Boolean,
    val vibrationEnabled: Boolean,
    val rowFontSize: Float
) {
    companion object {
        const val DEFAULT_FONT_SIZE = 16f
        val DEFAULT = AppSettings(
            xmlUrl = "http://live.kapa.lv/startlis.XML",
            serviceBusConnectionString = "",
            serviceBusQueueName = "start-events",
            headerText = "Orienteering Start",
            prestartMinutes = -2,
            lateStartMinutes = 0,
            soundEnabled = false,
            vibrationEnabled = false,
            rowFontSize = DEFAULT_FONT_SIZE
        )
    }
}
