package com.orienteering.startref.ui.common

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Redo
import androidx.compose.material.icons.filled.Undo
import androidx.compose.material3.FloatingActionButton
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp

@Composable
fun UndoRedoButtons(
    canUndo: Boolean,
    canRedo: Boolean,
    onUndo: () -> Unit,
    onRedo: () -> Unit,
    modifier: Modifier = Modifier
) {
    Row(
        modifier = modifier,
        horizontalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        FloatingActionButton(
            onClick = onUndo,
            modifier = Modifier.size(48.dp),
            containerColor = if (canUndo) MaterialTheme.colorScheme.primary
                             else MaterialTheme.colorScheme.surfaceVariant
        ) {
            Icon(Icons.Default.Undo, contentDescription = "Undo",
                tint = if (canUndo) MaterialTheme.colorScheme.onPrimary
                       else MaterialTheme.colorScheme.onSurfaceVariant)
        }
        FloatingActionButton(
            onClick = onRedo,
            modifier = Modifier.size(48.dp),
            containerColor = if (canRedo) MaterialTheme.colorScheme.primary
                             else MaterialTheme.colorScheme.surfaceVariant
        ) {
            Icon(Icons.Default.Redo, contentDescription = "Redo",
                tint = if (canRedo) MaterialTheme.colorScheme.onPrimary
                       else MaterialTheme.colorScheme.onSurfaceVariant)
        }
    }
}
