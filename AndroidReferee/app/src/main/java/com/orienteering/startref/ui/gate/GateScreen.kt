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
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
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

private val SignalWhite = Color.White
private val SignalBrightGreen = Color(0xFF00E676)
private val SignalGreen = Color(0xFF4CAF50)
private val SignalOrange = Color(0xFFFF9800)
private val SignalRed = Color(0xFFF44336)

@Composable
fun GateScreen(viewModel: GateViewModel = hiltViewModel()) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()
    val settings by viewModel.settings.collectAsStateWithLifecycle()
    val canUndo by viewModel.canUndo.collectAsStateWithLifecycle()
    val canRedo by viewModel.canRedo.collectAsStateWithLifecycle()
    val syncCounts by viewModel.syncCounts.collectAsStateWithLifecycle()

    Scaffold { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
        ) {
            // SI Reader connection status strip
            SiStatusStrip(connected = state.readerConnected)

            // Clock + current-minute label — isolated in its own composable so the
            // per-second tick does not recompose the runner list below.
            GateClockSection(viewModel = viewModel, signal = state.signal)

            LazyColumn(modifier = Modifier.weight(1f)) {
                items(
                    items = state.currentMinuteRunners,
                    key = { it.startNumber },
                    contentType = { "runner" }
                ) { runner ->
                    RunnerGateRow(
                        runner = runner,
                        isStarted = runner.statusId == 2,
                        isJustMatched = runner.startNumber == state.lastMatchedRunner?.startNumber && state.signal == GateSignal.BRIGHT_GREEN,
                        clickable = state.rowsClickable,
                        fontSize = settings.gateFontSize,
                        onClick = viewModel::assignChipToRunner
                    )
                }
            }

            HorizontalDivider()

            // Action buttons — visible on ORANGE or RED
            if (state.showActionButtons) {
                Row(
                    modifier = Modifier
                        .fillMaxWidth()
                        .padding(horizontal = 16.dp, vertical = 8.dp),
                    horizontalArrangement = Arrangement.spacedBy(12.dp)
                ) {
                    when (state.signal) {
                        GateSignal.ORANGE -> {
                            OutlinedButton(
                                onClick = { viewModel.dontLetIn() },
                                modifier = Modifier.weight(1f)
                            ) {
                                Text("Don't let in")
                            }
                            Button(
                                onClick = { viewModel.approve() },
                                modifier = Modifier.weight(1f),
                                colors = ButtonDefaults.buttonColors(containerColor = Color(0xFF4CAF50))
                            ) {
                                Text("Mark as started")
                            }
                            OutlinedButton(
                                onClick = { viewModel.dismiss() },
                                modifier = Modifier.weight(1f)
                            ) {
                                Text("Assign to runner")
                            }
                        }
                        GateSignal.RED -> {
                            OutlinedButton(
                                onClick = { viewModel.dontLetIn() },
                                modifier = Modifier.weight(1f)
                            ) {
                                Text("Don't let in")
                            }
                        }
                        else -> {}
                    }
                }
            }

            // Undo/Redo controls + sync-count indicator
            val (sent, total) = syncCounts
            Row(
                modifier = Modifier
                    .align(Alignment.End)
                    .padding(8.dp),
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                Box(
                    modifier = Modifier
                        .height(48.dp)
                        .widthIn(min = 48.dp)
                        .background(Color.White, shape = RoundedCornerShape(6.dp))
                        .clickable { viewModel.forcePush() }
                        .padding(horizontal = 8.dp),
                    contentAlignment = Alignment.Center
                ) {
                    Text(
                        text = "$sent/$total",
                        style = MaterialTheme.typography.labelSmall,
                        maxLines = 1,
                        color = if (sent < total) Color(0xFFE65100) else Color(0xFF2E7D32)
                    )
                }
                UndoRedoButtons(
                    canUndo = canUndo,
                    canRedo = canRedo,
                    onUndo = { viewModel.undo() },
                    onRedo = { viewModel.redo() }
                )
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
private fun GateClockSection(viewModel: GateViewModel, signal: GateSignal) {
    val adjustedMs by viewModel.adjustedCurrentTimeMs.collectAsStateWithLifecycle()

    // Time field — background color = signal color
    TimeField(
        timeMs = adjustedMs,
        signal = signal,
        modifier = Modifier.fillMaxWidth()
    )

    HorizontalDivider()

    val minuteLabel = remember(adjustedMs / 60_000) {
        clockFormatter.format(
            Instant.ofEpochMilli((adjustedMs / 60_000) * 60_000)
        ).substring(0, 5) // HH:mm
    }
    Text(
        text = "Runners starting at $minuteLabel",
        style = MaterialTheme.typography.titleSmall,
        modifier = Modifier.padding(horizontal = 16.dp, vertical = 8.dp)
    )
}

@Composable
private fun TimeField(timeMs: Long, signal: GateSignal, modifier: Modifier = Modifier) {
    val bgColor = when (signal) {
        GateSignal.IDLE -> SignalWhite
        GateSignal.BRIGHT_GREEN -> SignalBrightGreen
        GateSignal.GREEN -> SignalGreen
        GateSignal.ORANGE -> SignalOrange
        GateSignal.RED -> SignalRed
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
    onClick: (RunnerEntity) -> Unit
) {
    val bgColor = when {
        isJustMatched -> SignalBrightGreen   // bright green flash
        isStarted     -> SignalGreen         // steady green — runner has started
        else          -> Color.Transparent
    }
    val normalStyle = remember(fontSize) {
        TextStyle(fontSize = fontSize.sp, fontWeight = FontWeight.Bold)
    }
    val narrowStyle = remember(fontSize) {
        normalStyle.copy(textGeometricTransform = TextGeometricTransform(scaleX = 0.7f))
    }
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(bgColor)
            .clickable(enabled = clickable, onClick = { onClick(runner) })
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
