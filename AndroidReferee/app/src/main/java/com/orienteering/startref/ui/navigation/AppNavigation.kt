package com.orienteering.startref.ui.navigation

import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.BugReport
import androidx.compose.material.icons.filled.List
import androidx.compose.material.icons.filled.SensorDoor
import androidx.compose.material.icons.filled.Settings
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Icon
import androidx.compose.material3.NavigationBar
import androidx.compose.material3.NavigationBarItem
import androidx.compose.material3.Scaffold
import androidx.compose.material3.Text
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.compose.foundation.layout.height
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.navigation.NavDestination.Companion.hierarchy
import androidx.navigation.NavGraph.Companion.findStartDestination
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.rememberNavController
import com.orienteering.startref.ui.debug.DebugScreen
import com.orienteering.startref.ui.gate.GateScreen
import com.orienteering.startref.ui.settings.SettingsScreen
import com.orienteering.startref.ui.startlist.StartListScreen

@Composable
fun AppNavigation() {
    val navController = rememberNavController()
    val navBackStackEntry by navController.currentBackStackEntryAsState()
    val currentDestination = navBackStackEntry?.destination

    Scaffold(
        bottomBar = {
            NavigationBar(modifier = Modifier.height(80.dp)) {
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
        }
    }
}
