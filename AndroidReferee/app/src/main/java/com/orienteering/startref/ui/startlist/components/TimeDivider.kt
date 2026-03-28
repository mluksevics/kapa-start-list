package com.orienteering.startref.ui.startlist.components

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.TextUnit
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.orienteering.startref.ui.theme.TimeDividerBlue
import com.orienteering.startref.ui.theme.TimeDividerYellow
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter

private val timeFormatter = DateTimeFormatter.ofPattern("HH:mm:ss").withZone(ZoneId.systemDefault())

@Composable
fun TimeDivider(timeMinute: Long, isCurrent: Boolean, fontSize: TextUnit = 16.sp) {
    val bg = if (isCurrent) TimeDividerYellow else TimeDividerBlue
    val timeLabel = timeFormatter.format(Instant.ofEpochMilli(timeMinute))
    val label = if (isCurrent) "$timeLabel  (now at the line)" else timeLabel

    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(bg)
            .padding(horizontal = 12.dp, vertical = 6.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Text(
            text = label,
            fontSize = fontSize,
            fontWeight = FontWeight.Bold
        )
    }
}
