package com.orienteering.startref.ui.startlist

import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.lazy.rememberLazyListState
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.GpsFixed
import androidx.compose.material.icons.filled.GpsNotFixed
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.LinearProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Snackbar
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.orienteering.startref.ui.common.UndoRedoButtons
import com.orienteering.startref.ui.edituser.ChipQuickEditDialog
import com.orienteering.startref.ui.edituser.EditUserDialog
import com.orienteering.startref.ui.startlist.components.RunnerRow
import com.orienteering.startref.ui.startlist.components.TimeDivider
import kotlinx.coroutines.launch
import java.time.Instant
import java.time.ZoneId
import java.time.format.DateTimeFormatter

private val clockFormatter = DateTimeFormatter.ofPattern("HH:mm:ss").withZone(ZoneId.systemDefault())

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun StartListScreen(
    viewModel: StartListViewModel = hiltViewModel()
) {
    val items by viewModel.startListItems.collectAsStateWithLifecycle()
    val currentTimeMs by viewModel.currentTimeMs.collectAsStateWithLifecycle()
    val settings by viewModel.settings.collectAsStateWithLifecycle()
    val selectedRunner by viewModel.selectedRunner.collectAsStateWithLifecycle()
    val chipQuickEditRunner by viewModel.chipQuickEditRunner.collectAsStateWithLifecycle()
    val isLoading by viewModel.isLoading.collectAsStateWithLifecycle()
    val message by viewModel.message.collectAsStateWithLifecycle()
    val availableClasses by viewModel.availableClasses.collectAsStateWithLifecycle()
    val availableClubs by viewModel.availableClubs.collectAsStateWithLifecycle()
    val autoScrollEnabled by viewModel.autoScrollEnabled.collectAsStateWithLifecycle()
    val syncCounts by viewModel.syncCounts.collectAsStateWithLifecycle()
    val isSyncing by viewModel.isSyncing.collectAsStateWithLifecycle()

    val highlightedStartNumber by viewModel.highlightedStartNumber.collectAsStateWithLifecycle()
    val readerConnected by viewModel.readerConnected.collectAsStateWithLifecycle()
    val canUndo by viewModel.canUndo.collectAsStateWithLifecycle()
    val canRedo by viewModel.canRedo.collectAsStateWithLifecycle()

    val listState = rememberLazyListState()
    val scope = rememberCoroutineScope()
    val snackbarHostState = remember { SnackbarHostState() }

    LaunchedEffect(message) {
        message?.let {
            snackbarHostState.showSnackbar(it)
            viewModel.clearMessage()
        }
    }

    // Auto-scroll to SI-highlighted runner
    LaunchedEffect(highlightedStartNumber) {
        val startNr = highlightedStartNumber ?: return@LaunchedEffect
        val idx = items.indexOfFirst { it is StartListItem.Row && it.runner.startNumber == startNr }
        if (idx >= 0) listState.animateScrollToItem(idx)
    }

    // Auto-scroll: fire every time the minute changes
    val currentMinute = currentTimeMs / 60_000
    LaunchedEffect(currentMinute) {
        if (autoScrollEnabled && items.isNotEmpty()) {
            val idx = viewModel.currentHeaderIndex()
            if (idx >= 0) listState.animateScrollToItem(idx)
        }
    }

    Scaffold(
        topBar = {
            Column {
                TopAppBar(
                    navigationIcon = {
                        // Sync counter with white circle background
                        val (sent, total) = syncCounts
                        Box(
                            modifier = Modifier
                                .padding(start = 8.dp)
                                .size(40.dp)
                                .background(Color.White, shape = CircleShape),
                            contentAlignment = Alignment.Center
                        ) {
                            Text(
                                text = "$sent/$total",
                                style = MaterialTheme.typography.labelSmall,
                                color = if (sent < total) Color(0xFFE65100) else Color(0xFF2E7D32),
                                maxLines = 1
                            )
                        }
                    },
                    title = {
                        Row(verticalAlignment = Alignment.CenterVertically) {
                            val adjustedTimeMs = currentTimeMs -
                                (settings.prestartMinutes * 60_000L)
                            Text(
                                clockFormatter.format(Instant.ofEpochMilli(adjustedTimeMs)),
                                style = MaterialTheme.typography.titleMedium
                            )
                            Spacer(modifier = Modifier.width(10.dp))
                            Text(settings.headerText, style = MaterialTheme.typography.titleMedium)
                        }
                    },
                    actions = {
                        // Auto-scroll toggle
                        IconButton(onClick = { viewModel.toggleAutoScroll() }) {
                            Icon(
                                imageVector = if (autoScrollEnabled) Icons.Default.GpsFixed else Icons.Default.GpsNotFixed,
                                contentDescription = if (autoScrollEnabled) "Auto-scroll ON" else "Auto-scroll OFF",
                                tint = if (autoScrollEnabled) Color(0xFF76FF03) else MaterialTheme.colorScheme.onPrimary
                            )
                        }

                        // Manual scroll to NOW
                        TextButton(onClick = {
                            scope.launch {
                                val idx = viewModel.currentHeaderIndex()
                                if (idx >= 0) listState.animateScrollToItem(idx)
                            }
                        }) {
                            Text(
                                "Scroll\nto NOW",
                                style = MaterialTheme.typography.labelSmall,
                                color = MaterialTheme.colorScheme.onPrimary
                            )
                        }

                    },
                    colors = TopAppBarDefaults.topAppBarColors(
                        containerColor = MaterialTheme.colorScheme.primary,
                        titleContentColor = MaterialTheme.colorScheme.onPrimary,
                        actionIconContentColor = MaterialTheme.colorScheme.onPrimary,
                        navigationIconContentColor = MaterialTheme.colorScheme.onPrimary
                    )
                )
                SiStatusStrip(connected = readerConnected)
                if (isSyncing) {
                    LinearProgressIndicator(modifier = Modifier.fillMaxWidth())
                }
            }
        },
        snackbarHost = {
            SnackbarHost(snackbarHostState) { data ->
                Snackbar(modifier = Modifier.padding(8.dp)) { Text(data.visuals.message) }
            }
        }
    ) { innerPadding ->
        Box(
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding)
        ) {
            if (items.isEmpty() && !isLoading) {
                Text(
                    "No start list loaded. Go to Settings -> Force Pull all.",
                    modifier = Modifier
                        .align(Alignment.Center)
                        .padding(32.dp),
                    style = MaterialTheme.typography.bodyMedium
                )
            } else {
                LazyColumn(
                    state = listState,
                    modifier = Modifier.fillMaxSize()
                ) {
                    items(
                        items = items,
                        key = { item ->
                            when (item) {
                                is StartListItem.Header -> "h_${item.timeMinute}"
                                is StartListItem.Row -> "r_${item.runner.startNumber}"
                            }
                        }
                    ) { item ->
                        Box {
                            when (item) {
                                is StartListItem.Header -> TimeDivider(item.timeMinute, item.isCurrent, settings.rowFontSize.sp)
                                is StartListItem.Row -> RunnerRow(
                                    runner = item.runner,
                                    highlightFields = item.highlightFields,
                                    highlighted = item.runner.startNumber == highlightedStartNumber,
                                    onCheckIn = { viewModel.toggleStarted(item.runner.startNumber) },
                                    onDns = { viewModel.toggleDns(item.runner.startNumber) },
                                    onEdit = { viewModel.selectRunner(item.runner) },
                                    onChipClick = { viewModel.openChipQuickEdit(item.runner) },
                                    fontSize = settings.rowFontSize.sp
                                )
                            }
                        }
                    }
                }
            }

            if (isLoading) {
                CircularProgressIndicator(modifier = Modifier.align(Alignment.Center))
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
        }
    }

    selectedRunner?.let { runner ->
        EditUserDialog(
            runner = runner,
            availableClasses = availableClasses,
            availableClubs = availableClubs,
            currentTimeMs = currentTimeMs,
            onDismiss = { viewModel.selectRunner(null) },
            onSave = { updated -> viewModel.updateRunner(updated) }
        )
    }

    chipQuickEditRunner?.let { runner ->
        ChipQuickEditDialog(
            runner = runner,
            onDismiss = { viewModel.clearChipQuickEdit() },
            onSave = { digits -> viewModel.saveQuickChip(digits) }
        )
    }
}

@Composable
private fun SiStatusStrip(connected: Boolean) {
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
