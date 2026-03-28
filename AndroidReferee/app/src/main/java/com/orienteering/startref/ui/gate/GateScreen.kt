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
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.getValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.orienteering.startref.data.local.entity.RunnerEntity
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter

private val clockFormatter = DateTimeFormatter.ofPattern("HH:mm:ss").withZone(ZoneId.systemDefault())

@Composable
fun GateScreen(viewModel: GateViewModel = hiltViewModel()) {
    val state by viewModel.uiState.collectAsStateWithLifecycle()

    DisposableEffect(Unit) {
        viewModel.onGateActive()
        onDispose { viewModel.onGateInactive() }
    }

    Scaffold { paddingValues ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues)
        ) {
            // Time field — background color = signal color
            TimeField(
                timeMs = state.currentTimeMs,
                signal = state.signal,
                modifier = Modifier.fillMaxWidth()
            )

            HorizontalDivider()

            // Current-minute runner list
            val minuteLabel = clockFormatter.format(
                Instant.ofEpochMilli((state.currentTimeMs / 60_000) * 60_000)
            ).substring(0, 5) // HH:mm

            Text(
                text = "Runners starting at $minuteLabel",
                style = MaterialTheme.typography.titleSmall,
                modifier = Modifier.padding(horizontal = 16.dp, vertical = 8.dp)
            )

            LazyColumn(modifier = Modifier.weight(1f)) {
                items(state.currentMinuteRunners) { runner ->
                    RunnerGateRow(
                        runner = runner,
                        signal = state.signal,
                        isMatched = runner.startNumber == state.lastMatchedRunner?.startNumber,
                        onClick = { viewModel.assignChipToRunner(runner) }
                    )
                }
            }

            HorizontalDivider()

            // Action buttons — visible only on ORANGE or RED
            if (state.signal == GateSignal.ORANGE || state.signal == GateSignal.RED) {
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
                        onClick = { viewModel.handleManually() },
                        modifier = Modifier.weight(1f)
                    ) {
                        Text("Handle Manually")
                    }
                }
            }

            // Status line
            Text(
                text = state.statusLine,
                style = MaterialTheme.typography.bodyMedium,
                modifier = Modifier
                    .fillMaxWidth()
                    .background(MaterialTheme.colorScheme.surfaceVariant)
                    .padding(12.dp)
            )
        }
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
    signal: GateSignal,
    isMatched: Boolean,
    onClick: () -> Unit
) {
    val bgColor = when {
        isMatched && signal == GateSignal.GREEN -> Color(0xFFE8F5E9)
        isMatched && signal == GateSignal.BRIGHT_GREEN -> Color(0xFFA5D6A7)
        else -> Color.Transparent
    }
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .background(bgColor)
            .clickable(enabled = signal == GateSignal.RED, onClick = onClick)
            .padding(horizontal = 16.dp, vertical = 10.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Text(
            text = "${runner.startNumber}",
            fontWeight = FontWeight.Bold,
            modifier = Modifier.padding(end = 12.dp),
            style = MaterialTheme.typography.bodyLarge
        )
        Column(modifier = Modifier.weight(1f)) {
            Text(
                text = "${runner.name} ${runner.surname}",
                style = MaterialTheme.typography.bodyLarge
            )
            Text(
                text = runner.className,
                style = MaterialTheme.typography.bodySmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant
            )
        }
        if (signal == GateSignal.RED) {
            Text(
                text = "Tap to assign",
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.primary
            )
        }
    }
    HorizontalDivider()
}
