package com.orienteering.startref.ui.navigation

import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.BugReport
import androidx.compose.material.icons.filled.HelpOutline
import androidx.compose.material.icons.filled.List
import androidx.compose.material.icons.filled.SensorDoor
import androidx.compose.material.icons.filled.Settings
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.ui.Modifier
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import androidx.navigation.NavDestination.Companion.hierarchy
import androidx.navigation.NavGraph.Companion.findStartDestination
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.rememberNavController
import com.orienteering.startref.ui.debug.DebugScreen
import com.orienteering.startref.ui.gate.GateScreen
import com.orienteering.startref.ui.help.HelpScreen
import com.orienteering.startref.ui.settings.SettingsScreen
import com.orienteering.startref.ui.settings.SettingsViewModel
import com.orienteering.startref.ui.startlist.StartListScreen
import kotlinx.coroutines.delay
import java.time.LocalDate

@Composable
fun AppNavigation() {
    val navController = rememberNavController()
    val navBackStackEntry by navController.currentBackStackEntryAsState()
    val currentDestination = navBackStackEntry?.destination

    val settingsViewModel: SettingsViewModel = hiltViewModel()
    val settings by settingsViewModel.settings.collectAsStateWithLifecycle()
    var showMidnightDialog by remember { mutableStateOf(false) }
    var newDate by remember { mutableStateOf("") }

    // Check every 30s if the date has rolled past midnight
    LaunchedEffect(Unit) {
        var lastKnownDate = LocalDate.now().toString()
        while (true) {
            delay(30_000)
            val today = LocalDate.now().toString()
            if (today != lastKnownDate && today != settings.competitionDate) {
                lastKnownDate = today
                newDate = today
                showMidnightDialog = true
            } else {
                lastKnownDate = today
            }
        }
    }

    if (showMidnightDialog) {
        AlertDialog(
            onDismissRequest = { showMidnightDialog = false },
            title = { Text("Date changed") },
            text = { Text("It is past midnight. The competition date is still ${settings.competitionDate}.\n\nSwitch to the new date ($newDate)?") },
            confirmButton = {
                Button(
                    onClick = {
                        settingsViewModel.updateCompetitionDate(newDate)
                        showMidnightDialog = false
                    }
                ) { Text("Switch to $newDate") }
            },
            dismissButton = {
                TextButton(onClick = { showMidnightDialog = false }) { Text("Keep current") }
            }
        )
    }

    Scaffold(
        bottomBar = {
            NavigationBar {
                NavigationBarItem(
                    selected = currentDestination?.hierarchy?.any { it.route == "startlist" } == true,
                    onClick = {
                        navController.navigate("startlist") {
                            popUpTo(navController.graph.findStartDestination().id) { saveState = true }
                            launchSingleTop = true
                            restoreState = true
                        }
                    },
                    icon = { Icon(Icons.Default.List, contentDescription = "Start List") },
                    label = { Text("Start List") }
                )
                NavigationBarItem(
                    selected = currentDestination?.hierarchy?.any { it.route == "gate" } == true,
                    onClick = {
                        navController.navigate("gate") {
                            popUpTo(navController.graph.findStartDestination().id) { saveState = true }
                            launchSingleTop = true
                            restoreState = true
                        }
                    },
                    icon = { Icon(Icons.Default.SensorDoor, contentDescription = "Gate") },
                    label = { Text("Gate") }
                )
                NavigationBarItem(
                    selected = currentDestination?.hierarchy?.any { it.route == "settings" } == true,
                    onClick = {
                        navController.navigate("settings") {
                            popUpTo(navController.graph.findStartDestination().id) { saveState = true }
                            launchSingleTop = true
                            restoreState = true
                        }
                    },
                    icon = { Icon(Icons.Default.Settings, contentDescription = "Settings") },
                    label = { Text("Settings") }
                )
                NavigationBarItem(
                    selected = currentDestination?.hierarchy?.any { it.route == "debug" } == true,
                    onClick = {
                        navController.navigate("debug") {
                            popUpTo(navController.graph.findStartDestination().id) { saveState = true }
                            launchSingleTop = true
                            restoreState = true
                        }
                    },
                    icon = { Icon(Icons.Default.BugReport, contentDescription = "Debug") },
                    label = { Text("Debug") }
                )
                NavigationBarItem(
                    selected = currentDestination?.hierarchy?.any { it.route == "help" } == true,
                    onClick = {
                        navController.navigate("help") {
                            popUpTo(navController.graph.findStartDestination().id) { saveState = true }
                            launchSingleTop = true
                            restoreState = true
                        }
                    },
                    icon = { Icon(Icons.Default.HelpOutline, contentDescription = "Help") },
                    label = { Text("Help") }
                )
            }
        }
    ) { innerPadding ->
        NavHost(
            navController = navController,
            startDestination = "startlist",
            modifier = Modifier.padding(bottom = innerPadding.calculateBottomPadding())
        ) {
            composable("startlist") {
                StartListScreen()
            }
            composable("gate") {
                GateScreen()
            }
            composable("settings") {
                SettingsScreen()
            }
            composable("debug") {
                DebugScreen()
            }
            composable("help") {
                HelpScreen()
            }
        }
    }
}
