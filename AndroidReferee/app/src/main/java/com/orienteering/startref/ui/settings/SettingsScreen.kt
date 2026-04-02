package com.orienteering.startref.ui.settings

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Snackbar
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.RadioButton
import androidx.compose.material3.Switch
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.orienteering.startref.BuildConfig
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun SettingsScreen(
    viewModel: SettingsViewModel = hiltViewModel()
) {
    val settings by viewModel.settings.collectAsStateWithLifecycle()
    val isLoading by viewModel.isLoading.collectAsStateWithLifecycle()
    val message by viewModel.message.collectAsStateWithLifecycle()
    val availableSerialDevices by viewModel.availableSerialDevices.collectAsStateWithLifecycle()
    val snackbarHostState = remember { SnackbarHostState() }
    var showClearConfirm by remember { mutableStateOf(false) }
    var showFinalClearConfirm by remember { mutableStateOf(false) }

    // Local form state — initialized once from the first real DataStore emission
    var apiBaseUrl by rememberSaveable { mutableStateOf("") }
    var apiKey by rememberSaveable { mutableStateOf("") }
    var headerText by rememberSaveable { mutableStateOf("") }
    var pollIntervalStr by rememberSaveable { mutableStateOf("") }
    var prestartStr by rememberSaveable { mutableStateOf("") }
    var lateStartStr by rememberSaveable { mutableStateOf("") }
    var deviceName by rememberSaveable { mutableStateOf("") }
    // NOT rememberSaveable — must reset to false on every navigation to this screen
    var fieldsReady by remember { mutableStateOf(false) }

    LaunchedEffect(Unit) {
        viewModel.refreshSerialDevices()
        if (!fieldsReady) {
            val s = viewModel.awaitSettings()
            apiBaseUrl = s.apiBaseUrl
            apiKey = s.apiKey
            headerText = s.headerText
            pollIntervalStr = s.pollIntervalSeconds.toString()
            prestartStr = s.prestartMinutes.toString()
            lateStartStr = s.lateStartMinutes.toString()
            deviceName = s.deviceName
            fieldsReady = true
        }
    }

    LaunchedEffect(message) {
        message?.let {
            snackbarHostState.showSnackbar(it)
            viewModel.clearMessage()
        }
    }

    if (showClearConfirm) {
        AlertDialog(
            onDismissRequest = { showClearConfirm = false },
            title = { Text("Clear Cache") },
            text = { Text("This will delete all runners and pending sync items.") },
            confirmButton = {
                Button(
                    onClick = {
                        showClearConfirm = false
                        showFinalClearConfirm = true
                    },
                    colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.error)
                ) { Text("Continue") }
            },
            dismissButton = { TextButton(onClick = { showClearConfirm = false }) { Text("Cancel") } }
        )
    }

    if (showFinalClearConfirm) {
        AlertDialog(
            onDismissRequest = { showFinalClearConfirm = false },
            title = { Text("Final Confirmation") },
            text = { Text("Are you absolutely sure? This cannot be undone.") },
            confirmButton = {
                Button(
                    onClick = {
                        showFinalClearConfirm = false
                        viewModel.clearCache()
                    },
                    colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.error)
                ) { Text("Yes, clear now") }
            },
            dismissButton = {
                TextButton(onClick = { showFinalClearConfirm = false }) { Text("Cancel") }
            }
        )
    }

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Settings") },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.primary,
                    titleContentColor = MaterialTheme.colorScheme.onPrimary,
                    navigationIconContentColor = MaterialTheme.colorScheme.onPrimary
                )
            )
        },
        snackbarHost = {
            SnackbarHost(snackbarHostState) { data ->
                Snackbar(modifier = Modifier.padding(8.dp)) { Text(data.visuals.message) }
            }
        }
    ) { innerPadding ->
        if (!fieldsReady) {
            Box(
                modifier = Modifier.fillMaxSize().padding(innerPadding),
                contentAlignment = androidx.compose.ui.Alignment.Center
            ) { CircularProgressIndicator() }
            return@Scaffold
        }

        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding)
                .padding(16.dp)
                .verticalScroll(rememberScrollState()),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                Button(
                    onClick = { viewModel.reloadStartList() },
                    modifier = Modifier.weight(1f),
                    enabled = !isLoading
                ) {
                    if (isLoading) CircularProgressIndicator()
                    else Text("Force Pull all")
                }
                Button(
                    onClick = { viewModel.forcePush() },
                    modifier = Modifier.weight(1f)
                ) {
                    Text("Flush pending updates")
                }
            }

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                Button(
                    onClick = { viewModel.pullClasses() },
                    modifier = Modifier.weight(1f),
                    enabled = !isLoading
                ) {
                    Text("Pull classes")
                }
                Button(
                    onClick = { viewModel.pullClubs() },
                    modifier = Modifier.weight(1f),
                    enabled = !isLoading
                ) {
                    Text("Pull clubs")
                }
            }

            SettingField(label = "Header") {
                OutlinedTextField(
                    value = headerText,
                    onValueChange = { headerText = it; viewModel.updateHeaderText(it) },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true
                )
            }

            SettingField(label = "Device name (sync / lastModifiedBy)") {
                OutlinedTextField(
                    value = deviceName,
                    onValueChange = { deviceName = it; viewModel.updateDeviceName(it) },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true,
                    placeholder = { Text("android-xxxx") }
                )
            }

            SettingField(label = "API base URL") {
                OutlinedTextField(
                    value = apiBaseUrl,
                    onValueChange = { apiBaseUrl = it; viewModel.updateApiBaseUrl(it) },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true
                )
            }

            SettingField(label = "API key") {
                OutlinedTextField(
                    value = apiKey,
                    onValueChange = { apiKey = it; viewModel.updateApiKey(it) },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true
                )
            }

            SettingField(label = "Pull interval (seconds)") {
                OutlinedTextField(
                    value = pollIntervalStr,
                    onValueChange = {
                        pollIntervalStr = it
                        it.toIntOrNull()?.takeIf { v -> v >= 5 }?.let { v ->
                            viewModel.updatePollIntervalSeconds(v)
                        }
                    },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true,
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number)
                )
            }

            SettingField(label = "Start Place") {
                Row(
                    modifier = Modifier.fillMaxWidth(),
                    horizontalArrangement = Arrangement.spacedBy(16.dp)
                ) {
                    listOf(0 to "All", 1 to "1", 2 to "2", 3 to "3").forEach { (place, label) ->
                        Row(
                            verticalAlignment = androidx.compose.ui.Alignment.CenterVertically
                        ) {
                            RadioButton(
                                selected = settings.startPlace == place,
                                onClick = { viewModel.updateStartPlace(place) }
                            )
                            Text(text = label)
                        }
                    }
                }
            }

            SettingField(label = "Prestart (minutes, e.g. -2)") {
                OutlinedTextField(
                    value = prestartStr,
                    onValueChange = {
                        prestartStr = it
                        it.toIntOrNull()?.let { v -> viewModel.updatePrestartMinutes(v) }
                    },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true,
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number)
                )
            }

            SettingField(label = "Late start adjustment (minutes, any size — use large value to test scroll)") {
                OutlinedTextField(
                    value = lateStartStr,
                    onValueChange = {
                        lateStartStr = it
                        it.toIntOrNull()?.let { v -> viewModel.updateLateStartMinutes(v) }
                    },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true,
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number)
                )
            }

            SettingField(label = "Sound alert on minute change") {
                Switch(
                    checked = settings.soundEnabled,
                    onCheckedChange = { viewModel.updateSoundEnabled(it) }
                )
            }

            SettingField(label = "Vibration alert on minute change") {
                Switch(
                    checked = settings.vibrationEnabled,
                    onCheckedChange = { viewModel.updateVibrationEnabled(it) }
                )
            }

            SettingField(label = "Row font size: ${settings.rowFontSize.toInt()} sp") {
                Row(
                    verticalAlignment = androidx.compose.ui.Alignment.CenterVertically,
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Text("12", style = MaterialTheme.typography.labelSmall)
                    androidx.compose.material3.Slider(
                        value = settings.rowFontSize,
                        onValueChange = { viewModel.updateRowFontSize(it) },
                        valueRange = 12f..28f,
                        steps = 15,
                        modifier = Modifier.weight(1f).padding(horizontal = 8.dp)
                    )
                    Text("28", style = MaterialTheme.typography.labelSmall)
                }
            }

            SiReaderField(
                selectedKey = settings.siReaderDeviceKey,
                devices = availableSerialDevices,
                onRefresh = { viewModel.refreshSerialDevices() },
                onSelect = { viewModel.updateSiReaderDeviceKey(it) }
            )

            Button(
                onClick = { showClearConfirm = true },
                modifier = Modifier.fillMaxWidth(),
                colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.error)
            ) {
                Text("Clear cache")
            }

            Text(
                text = "v${BuildConfig.VERSION_NAME}",
                style = MaterialTheme.typography.labelSmall,
                color = MaterialTheme.colorScheme.onSurfaceVariant,
                modifier = Modifier.fillMaxWidth()
            )
        }
    }
}

@Composable
private fun SettingField(label: String, content: @Composable () -> Unit) {
    Column(modifier = Modifier.fillMaxWidth()) {
        Text(
            text = label,
            style = MaterialTheme.typography.labelMedium,
            color = MaterialTheme.colorScheme.primary
        )
        Spacer(modifier = Modifier.height(4.dp))
        content()
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun SiReaderField(
    selectedKey: String,
    devices: List<Pair<String, String>>,
    onRefresh: () -> Unit,
    onSelect: (String) -> Unit
) {
    val autoLabel = "Auto (first available)"
    val allOptions = listOf("" to autoLabel) + devices
    val selectedLabel = allOptions.firstOrNull { it.first == selectedKey }?.second ?: autoLabel

    var expanded by remember { mutableStateOf(false) }

    SettingField(label = "SI Reader port") {
        Row(
            modifier = Modifier.fillMaxWidth(),
            verticalAlignment = Alignment.CenterVertically,
            horizontalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            ExposedDropdownMenuBox(
                expanded = expanded,
                onExpandedChange = { expanded = it },
                modifier = Modifier.weight(1f)
            ) {
                OutlinedTextField(
                    value = if (devices.isEmpty() && selectedKey.isEmpty()) "(none connected)" else selectedLabel,
                    onValueChange = {},
                    readOnly = true,
                    trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(expanded = expanded) },
                    modifier = Modifier
                        .menuAnchor()
                        .fillMaxWidth(),
                    singleLine = true
                )
                ExposedDropdownMenu(
                    expanded = expanded,
                    onDismissRequest = { expanded = false }
                ) {
                    allOptions.forEach { (key, label) ->
                        DropdownMenuItem(
                            text = { Text(label) },
                            onClick = {
                                onSelect(key)
                                expanded = false
                            }
                        )
                    }
                }
            }
            TextButton(onClick = onRefresh) {
                Text("Refresh")
            }
        }
    }
}
