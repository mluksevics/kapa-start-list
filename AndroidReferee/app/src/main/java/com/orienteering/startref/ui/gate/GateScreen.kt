package com.orienteering.startref.ui.gate

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.TextStyle
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextGeometricTransform
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.orienteering.startref.data.local.entity.RunnerEntity
import com.orienteering.startref.ui.common.UndoRedoButtons
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter

private val clockFormatter = DateTimeFormatter.ofPattern("HH:mm:ss").withZone(ZoneId.systemDefault())

@Composable
fun GateScreen(viewModel: GateViewModel = hiltViewModel()) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    val settings by viewModel.settings.collectAsStateWithLifecycle()
    val canUndo by viewModel.canUndo.collectAsStateWithLifecycle()
    val canRedo by viewModel.canRedo.collectAsStateWithLifecycle()
    val syncCounts by viewModel.syncCounts.collectAsStateWithLifecycle()

    Scaffold { paddingValues ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
        ) {
        Column(
            modifier = Modifier.fillMaxSize()
        ) {
            // SI Reader connection status strip
            SiStatusStrip(connected = state.readerConnected)

            // Time field — background color = signal color
            TimeField(
                timeMs = state.adjustedCurrentTimeMs,
                signal = state.signal,
                modifier = Modifier.fillMaxWidth()
            )

            HorizontalDivider()

            // Current-minute runner list
            val minuteLabel = clockFormatter.format(
                Instant.ofEpochMilli((state.adjustedCurrentTimeMs / 60_000) * 60_000)
            ).substring(0, 5) // HH:mm

            Text(
                text = "Runners starting at $minuteLabel",
                style = MaterialTheme.typography.titleSmall,
                modifier = Modifier.padding(horizontal = 16.dp, vertical = 8.dp)
            )

            LazyColumn(modifier = Modifier.weight(1f)) {
                items(
                    items = state.currentMinuteRunners,
                    key = { it.startNumber }
                ) { runner ->
                    RunnerGateRow(
                        runner = runner,
                        isStarted = runner.statusId == 2,
                        isJustMatched = runner.startNumber == state.lastMatchedRunner?.startNumber && state.signal == GateSignal.BRIGHT_GREEN,
                        clickable = state.rowsClickable,
                        fontSize = settings.gateFontSize,
                        onClick = { viewModel.assignChipToRunner(runner) }
                    )
                }
            }

            HorizontalDivider()

            // Action buttons — visible on ORANGE or RED (includes manual-hold RED)
            if (state.rowsClickable) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = 16.dp, vertical = 8.dp),
                    horizontalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    if (state.signal == GateSignal.ORANGE) {
                        Button(
                            onClick = { viewModel.approve() },
                            modifier = Modifier.weight(1f),
                            colors = ButtonDefaults.buttonColors(containerColor = Color(0xFFFF9800))
                        ) {
                            Text("Approve")
                        }
                    }
                    OutlinedButton(
                        onClick = { viewModel.dismiss() },
                        modifier = Modifier.weight(1f)
                    ) {
                        Text("Dismiss")
                    }
                }
            }

            // Status line
            Text(
                text = state.statusLine,
                fontSize = 21.sp,
                modifier = Modifier
                    .fillMaxWidth()
                    .background(MaterialTheme.colorScheme.surfaceVariant)
                    .padding(12.dp)
            )
        } // end Column

        val (sent, total) = syncCounts
        Box(
            modifier = Modifier
                .align(Alignment.BottomStart)
                .padding(16.dp)
                .size(48.dp)
                .background(Color.White, shape = CircleShape)
                .clickable { viewModel.forcePush() },
            contentAlignment = Alignment.Center
        ) {
            Text(
                text = "$sent/$total",
                style = MaterialTheme.typography.labelSmall,
                color = if (sent < total) Color(0xFFE65100) else Color(0xFF2E7D32)
            )
        }

        UndoRedoButtons(
            canUndo = canUndo,
            canRedo = canRedo,
            onUndo = { viewModel.undo() },
            onRedo = { viewModel.redo() },
            modifier = Modifier
                .align(Alignment.BottomEnd)
                .padding(16.dp)
        )
        } // end Box
    }
}

@Composable
internal fun SiStatusStrip(connected: Boolean) {
    val dotColor = if (connected) Color(0xFF4CAF50) else Color(0xFFF44336)
    val label = if (connected) "SI Reader connected" else "SI Reader disconnected"
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(MaterialTheme.colorScheme.surfaceVariant)
            .padding(horizontal = 12.dp, vertical = 4.dp),
        verticalAlignment = Alignment.CenterVertically,
        horizontalArrangement = Arrangement.spacedBy(6.dp)
    ) {
        Box(
            modifier = Modifier
                .size(10.dp)
                .clip(CircleShape)
                .background(dotColor)
        )
        Text(text = label, style = MaterialTheme.typography.labelSmall)
    }
}

@Composable
private fun TimeField(timeMs: Long, signal: GateSignal, modifier: Modifier = Modifier) {
    val bgColor = when (signal) {
        GateSignal.IDLE -> Color.White
        GateSignal.BRIGHT_GREEN -> Color(0xFF00E676)
        GateSignal.GREEN -> Color(0xFF4CAF50)
        GateSignal.ORANGE -> Color(0xFFFF9800)
        GateSignal.RED -> Color(0xFFF44336)
    }
    val textColor = when (signal) {
        GateSignal.IDLE -> Color.Black
        else -> Color.White
    }

    Box(
        contentAlignment = Alignment.Center,
        modifier = modifier
            .background(bgColor)
            .padding(vertical = 24.dp)
    ) {
        Text(
            text = clockFormatter.format(Instant.ofEpochMilli(timeMs)),
            fontSize = 48.sp,
            fontWeight = FontWeight.Bold,
            color = textColor
        )
    }
}

@Composable
private fun RunnerGateRow(
    runner: RunnerEntity,
    isStarted: Boolean,       // statusId==2 from DB — persists across clock ticks
    isJustMatched: Boolean,   // bright flash for the current scan
    clickable: Boolean,
    fontSize: Float = 34f,
    onClick: () -> Unit
) {
    val bgColor = when {
        isJustMatched -> Color(0xFF00E676)   // bright green flash
        isStarted     -> Color(0xFF4CAF50)   // steady green — runner has started
        else          -> Color.Transparent
    }
    val normalStyle = TextStyle(fontSize = fontSize.sp, fontWeight = FontWeight.Bold)
    val narrowStyle = normalStyle.copy(textGeometricTransform = TextGeometricTransform(scaleX = 0.7f))
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(bgColor)
            .clickable(enabled = clickable, onClick = onClick)
            .padding(horizontal = 16.dp, vertical = 10.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Text(text = "${runner.startNumber}", style = normalStyle, modifier = Modifier.padding(end = 8.dp))
        Text(text = "${runner.name} ${runner.surname}", style = narrowStyle, modifier = Modifier.weight(1f))
        Text(text = runner.className, style = normalStyle, modifier = Modifier.padding(horizontal = 8.dp))
        Text(text = runner.siCard, style = normalStyle, modifier = Modifier.padding(start = 8.dp))
        if (clickable) {
            Text(
                text = "↑assign",
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.primary,
                modifier = Modifier.padding(start = 8.dp)
            )
        }
    }
    HorizontalDivider()
}
