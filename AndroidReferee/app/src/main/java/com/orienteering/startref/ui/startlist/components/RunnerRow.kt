package com.orienteering.startref.ui.startlist.components

import androidx.compose.foundation.ExperimentalFoundationApi
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.combinedClickable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.material3.Checkbox
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.TextUnit
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.orienteering.startref.data.local.entity.RunnerEntity
import com.orienteering.startref.ui.theme.CheckedInGreen
import com.orienteering.startref.ui.theme.DnsRed

@OptIn(ExperimentalFoundationApi::class)
@Composable
fun RunnerRow(
    runner: RunnerEntity,
    highlightFields: Set<String> = emptySet(),
    highlighted: Boolean = false,
    onCheckIn: () -> Unit,
    onDns: () -> Unit,
    onEdit: () -> Unit,
    onChipClick: () -> Unit = {},
    fontSize: TextUnit = 16.sp
) {
    val isDns = runner.statusId == 3
    val isCheckedIn = runner.statusId == 2
    val bg = when {
        highlighted -> Color(0xFFFFF176)   // Yellow flash for SI card read
        isDns -> DnsRed
        isCheckedIn -> CheckedInGreen
        else -> Color.White
    }

    fun hl(field: String) = highlightFields.contains(field)

    val hlColor = MaterialTheme.colorScheme.primary.copy(alpha = 0.22f)
    val rowModifier = Modifier
        .fillMaxWidth()
        .background(bg)
        .then(
            if (hl("StartTime")) Modifier.background(MaterialTheme.colorScheme.tertiary.copy(alpha = 0.2f))
            else Modifier
        )
        .padding(vertical = 4.dp)

    Row(
        modifier = rowModifier,
        verticalAlignment = Alignment.CenterVertically
    ) {
        Box(
            modifier = Modifier
                .size(52.dp)
                .then(if (hl("StatusId")) Modifier.background(hlColor) else Modifier)
                .combinedClickable(
                    onClick = onCheckIn,
                    onLongClick = onDns
                ),
            contentAlignment = Alignment.Center
        ) {
            Checkbox(
                checked = isCheckedIn,
                onCheckedChange = null
            )
        }

        Text(
            text = "${runner.startNumber}",
            fontSize = fontSize,
            fontWeight = if (hl("StartPlace")) FontWeight.Bold else FontWeight.Normal,
            modifier = Modifier
                .width(56.dp)
                .then(if (hl("StartPlace")) Modifier.background(hlColor) else Modifier)
        )

        Text(
            text = runner.className,
            fontSize = fontSize,
            fontWeight = if (hl("ClassId")) FontWeight.Bold else FontWeight.Normal,
            modifier = Modifier
                .width(90.dp)
                .then(if (hl("ClassId")) Modifier.background(hlColor) else Modifier),
            maxLines = 1,
            overflow = TextOverflow.Ellipsis
        )

        Text(
            text = runner.siCard,
            fontSize = fontSize,
            fontWeight = if (hl("SiChipNo")) FontWeight.Bold else FontWeight.Normal,
            modifier = Modifier
                .width(96.dp)
                .then(if (hl("SiChipNo")) Modifier.background(hlColor) else Modifier)
                .clickable(onClick = onChipClick),
            maxLines = 1,
            overflow = TextOverflow.Ellipsis
        )

        val nameHl = hl("Name") || hl("Surname")
        Text(
            text = "${runner.name} ${runner.surname}",
            fontSize = fontSize,
            fontWeight = if (nameHl) FontWeight.Bold else FontWeight.Normal,
            modifier = Modifier
                .weight(1f)
                .then(if (nameHl) Modifier.background(hlColor) else Modifier),
            maxLines = 1,
            overflow = TextOverflow.Ellipsis
        )

        Text(
            text = runner.clubName,
            fontSize = fontSize,
            fontWeight = if (hl("ClubId")) FontWeight.Bold else FontWeight.Normal,
            modifier = Modifier
                .width(100.dp)
                .then(if (hl("ClubId")) Modifier.background(hlColor) else Modifier),
            maxLines = 1,
            overflow = TextOverflow.Ellipsis
        )

        TextButton(onClick = onEdit) {
            Text("Edit", fontSize = fontSize)
        }
    }
}
