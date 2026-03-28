package com.orienteering.startref.ui.edituser

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.foundation.verticalScroll
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.DropdownMenuItem
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.ExposedDropdownMenuBox
import androidx.compose.material3.ExposedDropdownMenuDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TimePicker
import androidx.compose.material3.TimePickerState
import androidx.compose.material3.rememberTimePickerState
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.unit.dp
import androidx.compose.ui.window.Dialog
import com.orienteering.startref.data.local.ClassEntry
import com.orienteering.startref.data.local.ClubEntry
import com.orienteering.startref.data.local.entity.RunnerEntity
import java.time.Instant
import java.time.LocalDateTime
import java.time.ZoneId

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun EditUserDialog(
    runner: RunnerEntity,
    availableClasses: List<ClassEntry>,
    availableClubs: List<ClubEntry>,
    currentTimeMs: Long,
    onDismiss: () -> Unit,
    onSave: (RunnerEntity) -> Unit
) {
    val initialDateTime = LocalDateTime.ofInstant(
        Instant.ofEpochMilli(runner.startTime),
        ZoneId.systemDefault()
    )

    var name by remember { mutableStateOf(runner.name) }
    var surname by remember { mutableStateOf(runner.surname) }
    var siCard by remember { mutableStateOf(runner.siCard) }
    var selectedClassEntry by remember { mutableStateOf(ClassEntry(runner.classId, runner.className)) }
    var classDropdownExpanded by remember { mutableStateOf(false) }
    var selectedClubEntry by remember { mutableStateOf(ClubEntry(runner.clubId, runner.clubName)) }
    var clubDropdownExpanded by remember { mutableStateOf(false) }
    var showTimePicker by remember { mutableStateOf(false) }

    val timePickerState = rememberTimePickerState(
        initialHour = initialDateTime.hour,
        initialMinute = initialDateTime.minute,
        is24Hour = true
    )

    fun nowPlusMinutes(minutes: Int): Long {
        val now = currentTimeMs
        val truncated = (now / 60_000) * 60_000
        return truncated + (minutes * 60_000L)
    }

    fun buildUpdatedRunner(): RunnerEntity {
        val existingDateTime = LocalDateTime.ofInstant(
            Instant.ofEpochMilli(runner.startTime),
            ZoneId.systemDefault()
        )
        val newDateTime = existingDateTime
            .withHour(timePickerState.hour)
            .withMinute(timePickerState.minute)
            .withSecond(0)
            .withNano(0)
        val newStartTime = newDateTime.atZone(ZoneId.systemDefault()).toInstant().toEpochMilli()

        return runner.copy(
            name = name.trim(),
            surname = surname.trim(),
            siCard = siCard.trim(),
            classId = selectedClassEntry.classId,
            className = selectedClassEntry.className,
            clubId = selectedClubEntry.clubId,
            clubName = selectedClubEntry.clubName,
            startTime = newStartTime
        )
    }

    if (showTimePicker) {
        Dialog(onDismissRequest = { showTimePicker = false }) {
            Column(modifier = Modifier.padding(16.dp)) {
                TimePicker(state = timePickerState)
                Row(modifier = Modifier.fillMaxWidth(), horizontalArrangement = Arrangement.End) {
                    TextButton(onClick = { showTimePicker = false }) { Text("Cancel") }
                    TextButton(onClick = { showTimePicker = false }) { Text("OK") }
                }
            }
        }
    }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Edit Runner") },
        text = {
            Column(
                modifier = Modifier
                    .fillMaxWidth()
                    .verticalScroll(rememberScrollState()),
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                Text(
                    "Start No: ${runner.startNumber}",
                    style = MaterialTheme.typography.labelLarge
                )

                OutlinedTextField(
                    value = name,
                    onValueChange = { name = it },
                    label = { Text("Name") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true
                )

                OutlinedTextField(
                    value = surname,
                    onValueChange = { surname = it },
                    label = { Text("Surname") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true
                )

                OutlinedTextField(
                    value = siCard,
                    onValueChange = { if (it.all { c -> c.isDigit() }) siCard = it },
                    label = { Text("SI Card") },
                    modifier = Modifier.fillMaxWidth(),
                    singleLine = true,
                    keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.NumberPassword)
                )

                ExposedDropdownMenuBox(
                    expanded = classDropdownExpanded,
                    onExpandedChange = { classDropdownExpanded = it }
                ) {
                    OutlinedTextField(
                        value = selectedClassEntry.className,
                        onValueChange = {},
                        readOnly = true,
                        label = { Text("Class") },
                        trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(classDropdownExpanded) },
                        modifier = Modifier
                            .menuAnchor()
                            .fillMaxWidth()
                    )
                    ExposedDropdownMenu(
                        expanded = classDropdownExpanded,
                        onDismissRequest = { classDropdownExpanded = false }
                    ) {
                        availableClasses.forEach { entry ->
                            DropdownMenuItem(
                                text = { Text(entry.className) },
                                onClick = {
                                    selectedClassEntry = entry
                                    classDropdownExpanded = false
                                }
                            )
                        }
                    }
                }

                ExposedDropdownMenuBox(
                    expanded = clubDropdownExpanded,
                    onExpandedChange = { clubDropdownExpanded = it }
                ) {
                    OutlinedTextField(
                        value = selectedClubEntry.clubName,
                        onValueChange = {},
                        readOnly = true,
                        label = { Text("Club") },
                        trailingIcon = { ExposedDropdownMenuDefaults.TrailingIcon(clubDropdownExpanded) },
                        modifier = Modifier
                            .menuAnchor()
                            .fillMaxWidth()
                    )
                    ExposedDropdownMenu(
                        expanded = clubDropdownExpanded,
                        onDismissRequest = { clubDropdownExpanded = false }
                    ) {
                        availableClubs.forEach { entry ->
                            DropdownMenuItem(
                                text = { Text(entry.clubName) },
                                onClick = {
                                    selectedClubEntry = entry
                                    clubDropdownExpanded = false
                                }
                            )
                        }
                    }
                }

                Spacer(modifier = Modifier.height(4.dp))

                Text("Start Time", style = MaterialTheme.typography.labelMedium)

                OutlinedButton(
                    onClick = { showTimePicker = true },
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Text(
                        "%02d:%02d".format(timePickerState.hour, timePickerState.minute),
                        style = MaterialTheme.typography.bodyLarge
                    )
                }

                Row(
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    listOf(1, 2, 3, 4).forEach { minutes ->
                        Button(
                            onClick = {
                                val newMs = nowPlusMinutes(minutes)
                                val newDt = LocalDateTime.ofInstant(
                                    Instant.ofEpochMilli(newMs),
                                    ZoneId.systemDefault()
                                )
                                timePickerState.hour = newDt.hour
                                timePickerState.minute = newDt.minute
                            },
                            modifier = Modifier.weight(1f),
                            colors = ButtonDefaults.buttonColors(
                                containerColor = MaterialTheme.colorScheme.secondaryContainer,
                                contentColor = MaterialTheme.colorScheme.onSecondaryContainer
                            ),
                            contentPadding = androidx.compose.foundation.layout.PaddingValues(4.dp)
                        ) {
                            Text("+${minutes}m", style = MaterialTheme.typography.labelSmall)
                        }
                    }
                }
            }
        },
        confirmButton = {
            Button(onClick = { onSave(buildUpdatedRunner()) }) { Text("Save") }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) { Text("Cancel") }
        }
    )
}

/**
 * Returns a group identifier for classes that are allowed to swap within the same group,
 * or null if the class is not in any editable group.
 *
 * Groups:
 *  - "diropen" : starts with DIR or Open
 *  - "youth8"  : starts with M8, W8, M08, W08
 */
private fun classGroupOf(className: String): String? = when {
    className.startsWith("DIR", ignoreCase = true) ||
        className.startsWith("Open", ignoreCase = true) -> "diropen"

    isYouth8Class(className) -> "youth8"

    else -> null
}

/**
 * Matches M8x / W8x / M08x / W08x youth classes where x is a letter or end of string.
 * Excludes age-group classes like M80, M85, W80, W85 where x is a digit.
 */
private fun isYouth8Class(className: String): Boolean {
    val upper = className.uppercase()
    for (prefix in listOf("M08", "W08", "M8", "W8")) {
        if (upper.startsWith(prefix)) {
            val afterPrefix = upper.drop(prefix.length)
            return afterPrefix.isEmpty() || !afterPrefix[0].isDigit()
        }
    }
    return false
}

