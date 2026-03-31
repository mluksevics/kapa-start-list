package com.orienteering.startref.ui.edituser

import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.text.input.KeyboardType
import kotlinx.coroutines.delay
import com.orienteering.startref.data.local.entity.RunnerEntity

@Composable
fun ChipQuickEditDialog(
    runner: RunnerEntity,
    onDismiss: () -> Unit,
    onSave: (String) -> Unit
) {
    var chip by remember(runner.startNumber) { mutableStateOf("") }
    val focusRequester = remember { FocusRequester() }
    LaunchedEffect(runner.startNumber) {
        delay(50)
        focusRequester.requestFocus()
    }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("SI chip — ${runner.startNumber}") },
        text = {
            OutlinedTextField(
                value = chip,
                onValueChange = { v ->
                    if (v.all { it.isDigit() }) chip = v
                },
                label = { Text("Chip number") },
                modifier = Modifier
                    .fillMaxWidth()
                    .focusRequester(focusRequester),
                singleLine = true,
                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Number)
            )
        },
        confirmButton = {
            Button(onClick = { onSave(chip.trim()) }) { Text("Save") }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) { Text("Cancel") }
        }
    )
}
