package com.orienteering.startref.ui.help

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ExpandLess
import androidx.compose.material.icons.filled.ExpandMore
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun HelpScreen() {
    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Help") },
                colors = TopAppBarDefaults.topAppBarColors(
                    containerColor = MaterialTheme.colorScheme.primary,
                    titleContentColor = MaterialTheme.colorScheme.onPrimary
                )
            )
        }
    ) { innerPadding ->
        Column(
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding)
                .padding(12.dp)
                .verticalScroll(rememberScrollState()),
            verticalArrangement = Arrangement.spacedBy(8.dp)
        ) {
            HelpSection("Overview") {
                HelpPara("StartRef keeps a start list in sync across multiple Android devices and the event PC (OE12/Desktop). The central .NET API is the single source of truth. This app lets referees mark runners as Started or DNS, edit SI chips, and operate the timing gate.")
                HelpPara("Changes made on this device are sent to the API immediately. If the network is unavailable the change is queued and retried automatically. Every ~30 seconds the app polls the API for updates from other devices.")
            }

            HelpSection("Start List tab") {
                HelpItem("Clock", "Shows the current time adjusted by the Prestart setting. The minute that is currently highlighted in the list matches this clock.")
                HelpItem("+Xms / -Xms", "Server clock offset next to the clock. Shows how far this device's clock differs from the server. Red if the difference exceeds 500 ms — PATCH timestamps may be unreliable in that case.")
                HelpItem("Sent/total circle", "Shows how many local changes have been successfully sent to the API. Orange when some are unsent; green when fully in sync. Tap to force-push pending updates immediately.")
                HelpItem("Auto-scroll (GPS icon)", "When enabled, the list scrolls to the current start minute every time the minute changes.")
                HelpItem("NOW button", "Scrolls the list to the current start minute once.")
                HelpItem("Tapping a runner row", "Opens the runner detail editor (name, surname, SI chip, class, club, start time, status).")
                HelpItem("Tapping the SI chip column", "Opens a quick SI chip entry dialog — faster than opening the full editor.")
                HelpItem("Minute dividers", "The currently active minute is highlighted. Runners in that minute are the ones expected at the gate right now.")
                HelpItem("Undo / Redo", "Floating buttons at the bottom-right. Undo reverses the last status change or edit and re-syncs to the API.")
                HelpItem("Field highlights", "After a delta poll, changed fields on a runner briefly glow to show what was updated by another device.")
            }

            HelpSection("Gate tab") {
                HelpPara("The gate screen reads SI chips via a USB OTG SportIdent station and signals whether the runner may start.")
                HelpItem("White (idle)", "Waiting for an SI chip scan.")
                HelpItem("Bright green → green", "SI chip matched a runner in the current start minute. Runner is marked Started automatically.")
                HelpItem("Orange", "SI chip matched a runner but in a different minute. Tap Approve to mark Started anyway, or Dismiss to keep the gate open and assign the chip manually by tapping a runner row.")
                HelpItem("Red", "SI chip not found in the start list, or Dismiss was tapped. Tap any runner row in the current minute to assign the chip to that runner.")
                HelpItem("Sent/total circle", "Same sync indicator as the Start List tab. Tap to force-push pending updates.")
                HelpItem("Undo / Redo", "Reverses the last assignment or status change from this screen.")
            }

            HelpSection("Settings") {
                HelpItem("Force Pull all", "Downloads the complete start list from the API and replaces local data. Use this after the Desktop has done a full OE12 import, or if local data looks corrupted.")
                HelpItem("Push pending updates", "Immediately retries all unsent PATCH requests. Normally these retry automatically; use this button when you want to flush them right now.")
                HelpItem("Pull classes / Pull clubs", "Re-fetches class and club lookup tables from the API. Normally done automatically when a runner with an unknown class or club is encountered during sync.")
                HelpItem("Header", "Text shown in the start list toolbar next to the clock. Typically the event or start place name.")
                HelpItem("Competition date", "The date used for all API calls (yyyy-MM-dd). Defaults to today. Shown in red when it differs from today. The app prompts automatically at midnight if the date has not been updated.")
                HelpItem("Device name", "Identifies this device in the lastModifiedBy field of every PATCH. Use a short unique name such as 'gate-1' or 'referee-2'. Conflicts between devices are resolved by timestamp — the most recent change wins.")
                HelpItem("API base URL", "Root URL of the StartRef API, e.g. https://startref.azurewebsites.net/")
                HelpItem("API key", "Secret key sent in the X-Api-Key header for all write requests.")
                HelpItem("Pull interval (seconds)", "How often the app polls the API for changes. Minimum 5 s. 30 s is typical.")
                HelpItem("Start Place", "Filters the start list and gate to show only runners assigned to a specific start location (1, 2, or 3). Select All to show every runner regardless of class start place.")
                HelpItem("Prestart (minutes)", "How many minutes before the official start time the runner is expected at the gate. A negative value (e.g. −2) means the gate clock runs 2 minutes ahead of the start time, so runners arriving 2 minutes early are matched to their minute. Set to 0 for no adjustment.")
                HelpItem("Sound alert", "Plays a beep at the start of each new minute on the start list.")
                HelpItem("Vibration alert", "Vibrates the device at the start of each new minute on the start list.")
                HelpItem("Loud sound", "Uses the device alarm stream (maximum volume, ignores the media/ringer volume slider). Effective for noisy start areas.")
                HelpItem("Row font size", "Font size for runner rows in the start list (12–28 sp).")
                HelpItem("Gate font size", "Font size for runner rows on the gate screen (16–48 sp). Increase for visibility at the gate.")
                HelpItem("SI Reader port", "Select the USB serial device to use as the SportIdent reader. Auto selects the first detected device. Tap Refresh after plugging in the station.")
                HelpItem("Clear cache", "Deletes all locally stored runners and the pending sync queue. Requires two confirmations. The start list must be re-pulled from the API afterwards.")
            }

            HelpSection("Sync & connectivity") {
                HelpPara("The app syncs in two directions:")
                HelpItem("Outgoing (PATCH)", "Every status change or edit is sent to the API immediately. On failure it is queued in a local database table and retried by WorkManager in the background. Retries use a fresh server-equivalent timestamp to avoid false conflict errors.")
                HelpItem("Incoming (poll)", "Every poll cycle: (1) fetches the server time via a lightweight /api/time call and computes a clock offset using NTP-style half-RTT compensation; (2) fetches only runners changed since the last watermark; (3) fetches class/club lookups only if a runner in the delta references an unknown ID.")
                HelpItem("Conflict resolution", "Last-write-wins by timestamp. If two devices edit the same runner within 5 seconds of each other the server accepts the later one; earlier ones receive a 409 and are kept in the pending queue for manual review via Push pending updates.")
                HelpItem("Clock offset indicator", "Visible in the start list toolbar. A large offset means this device's clock differs significantly from the server; PATCH timestamps may be slightly off. The offset is corrected automatically on the next poll.")
            }

            HelpSection("Debug tab") {
                HelpPara("The Debug tab shows a timestamped log of all HTTP requests and sync events. Useful for diagnosing connectivity issues at the event.")
                HelpItem("GET runners: Xms", "Duration of the delta poll request.")
                HelpItem("Time sync: offset=Xms RTT=Yms", "Server clock offset and round-trip time from the lightweight time-sync call.")
                HelpItem("Push #N OK (Xms)", "A PATCH for runner N was accepted by the API.")
                HelpItem("Push #N FAILED: HTTP 409 (Xms)", "The API rejected the PATCH because a newer version of the runner already exists on the server.")
                HelpItem("Clear button", "Clears the log.")
            }

            Spacer(modifier = Modifier.height(8.dp))
        }
    }
}

@Composable
private fun HelpSection(title: String, content: @Composable () -> Unit) {
    var expanded by rememberSaveable { mutableStateOf(false) }
    Card(
        modifier = Modifier.fillMaxWidth(),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
    ) {
        Column {
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .clickable { expanded = !expanded }
                    .padding(horizontal = 16.dp, vertical = 12.dp),
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = title,
                    style = MaterialTheme.typography.titleSmall,
                    fontWeight = FontWeight.SemiBold,
                    modifier = Modifier.weight(1f)
                )
                Icon(
                    imageVector = if (expanded) Icons.Default.ExpandLess else Icons.Default.ExpandMore,
                    contentDescription = null
                )
            }
            AnimatedVisibility(visible = expanded) {
                Column(modifier = Modifier.padding(horizontal = 16.dp, vertical = 8.dp)) {
                    content()
                }
            }
        }
    }
}

@Composable
private fun HelpItem(term: String, description: String) {
    Column(modifier = Modifier.padding(bottom = 10.dp)) {
        Text(
            text = term,
            style = MaterialTheme.typography.bodyMedium,
            fontWeight = FontWeight.SemiBold,
            color = MaterialTheme.colorScheme.primary
        )
        Text(
            text = description,
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurface
        )
    }
    HorizontalDivider(thickness = 0.5.dp, color = MaterialTheme.colorScheme.outlineVariant)
    Spacer(modifier = Modifier.height(6.dp))
}

@Composable
private fun HelpPara(text: String) {
    Text(
        text = text,
        style = MaterialTheme.typography.bodySmall,
        modifier = Modifier.padding(bottom = 10.dp)
    )
}
