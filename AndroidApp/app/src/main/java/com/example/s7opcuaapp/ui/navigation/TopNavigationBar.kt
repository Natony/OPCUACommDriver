package com.example.s7opcuaapp.ui.navigation

import androidx.compose.animation.animateColorAsState
import androidx.compose.animation.core.*
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ExitToApp
import androidx.compose.material.icons.filled.WifiOff
import androidx.compose.material.icons.filled.Wifi
import androidx.compose.material.icons.filled.ErrorOutline
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.navigation.NavController
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.navigation.compose.rememberNavController
import com.example.s7opcuaapp.R
import com.example.s7opcuaapp.ui.components.BatteryStatusItem
import com.example.s7opcuaapp.util.StatusLockConfig
import com.example.s7opcuaapp.viewmodel.ControlViewModel

@Composable
fun TopNavigationBar(
    navController: NavController,
    statusValue: Int = 0,
    batteryLevel: Int = 100,
    deviceName: String = "No Device",
    connectionState: ControlViewModel.ConnectionState,
    onLogout: () -> Unit = {}
) {
    val items = listOf(
        BottomNavItem.Control,
        BottomNavItem.Home,
        BottomNavItem.Alarm,
        BottomNavItem.Config
    )

    val navBackStackEntry by navController.currentBackStackEntryAsState()
    val currentRoute = navBackStackEntry?.destination?.route

    // Animated color for connection
    val connectionColor by animateColorAsState(
        targetValue = when (connectionState) {
            is ControlViewModel.ConnectionState.Connected -> Color(0xFF4CAF50)
            is ControlViewModel.ConnectionState.Connecting -> Color(0xFFFFC107)
            is ControlViewModel.ConnectionState.Failed -> Color(0xFFF44336)
            is ControlViewModel.ConnectionState.MaxRetriesExceeded -> Color(0xFF9C27B0)
            is ControlViewModel.ConnectionState.Timeout -> Color(0xFFFF5722)
            is ControlViewModel.ConnectionState.Offline -> Color(0xFF607D8B)
            else -> Color(0xFF9E9E9E)
        }, animationSpec = tween(300)
    )

    val connectionText = when (connectionState) {
        is ControlViewModel.ConnectionState.Connected -> "Connected"
        is ControlViewModel.ConnectionState.Connecting -> "Connecting... (${connectionState.attempt}/3)"
        is ControlViewModel.ConnectionState.Failed -> "Failed (${connectionState.attempt}/3)"
        is ControlViewModel.ConnectionState.MaxRetriesExceeded -> "Connection Failed"
        is ControlViewModel.ConnectionState.Timeout -> "Timeout"
        is ControlViewModel.ConnectionState.Offline -> "Offline Mode"
        else -> "Disconnected"
    }

    val connectionIcon = when (connectionState) {
        is ControlViewModel.ConnectionState.Connected -> Icons.Default.Wifi
        is ControlViewModel.ConnectionState.Offline -> Icons.Default.WifiOff
        is ControlViewModel.ConnectionState.Failed,
        is ControlViewModel.ConnectionState.MaxRetriesExceeded -> Icons.Default.ErrorOutline
        else -> Icons.Default.WifiOff
    }

    // Blink animation for connecting
    val infiniteTransition = rememberInfiniteTransition()
    val alpha by infiniteTransition.animateFloat(
        initialValue = 1f, targetValue = 0.3f,
        animationSpec = infiniteRepeatable(animation = tween(1000), repeatMode = RepeatMode.Reverse)
    )

    Surface(
        modifier = Modifier
            .fillMaxWidth()
            .height(64.dp),
        shadowElevation = 4.dp
    ) {
        Column(modifier = Modifier.fillMaxSize()) {
            Row(
                modifier = Modifier
                    .fillMaxWidth()
                    .height(64.dp)
                    .background(MaterialTheme.colorScheme.surfaceVariant),
                verticalAlignment = Alignment.CenterVertically
            ) {
                // Connection status + device (30%)
                Row(
                    modifier = Modifier
                        .weight(0.30f)
                        .fillMaxHeight()
                        .padding(horizontal = 8.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Icon(
                        imageVector = connectionIcon,
                        contentDescription = "Connection",
                        modifier = Modifier.size(20.dp),
                        tint = if (connectionState is ControlViewModel.ConnectionState.Connecting)
                            connectionColor.copy(alpha = alpha) else connectionColor
                    )
                    Spacer(modifier = Modifier.width(4.dp))
                    Text(
                        text = "$deviceName - $connectionText",
                        style = MaterialTheme.typography.labelSmall,
                        color = connectionColor,
                        fontWeight = FontWeight.Medium,
                        maxLines = 1,
                        overflow = TextOverflow.Ellipsis,
                        fontSize = 12.sp
                    )
                }

                // Status display (35%)
                Box(
                    modifier = Modifier.weight(0.35f),
                    contentAlignment = Alignment.Center
                ) {
                    Card(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(vertical = 4.dp),
                        colors = CardDefaults.cardColors(
                            containerColor = when (statusValue) {
                                0 -> Color(0xFFE0E0E0)
                                1 -> Color(0xFFC8E6C9)
                                in 2..6 -> Color(0xFFFFF9C4)
                                7 -> Color(0xFFA5D6A7)
                                11 -> Color(0xFFFFCDD2)
                                else -> MaterialTheme.colorScheme.surfaceVariant
                            }
                        )
                    ) {
                        Text(
                            text = StatusLockConfig.DEFAULT_STATUS_DESCRIPTIONS[statusValue] ?: "Unknown",
                            modifier = Modifier
                                .fillMaxWidth()
                                .padding(horizontal = 8.dp, vertical = 2.dp),
                            style = MaterialTheme.typography.labelSmall,
                            fontWeight = FontWeight.Medium,
                            textAlign = TextAlign.Center,
                            color = if (statusValue == 11) MaterialTheme.colorScheme.error
                            else MaterialTheme.colorScheme.onSurfaceVariant,
                            fontSize = 12.sp,
                            maxLines = 1
                        )
                    }
                }

                // Navigation icons (20%)
                Row(
                    modifier = Modifier.weight(0.20f),
                    horizontalArrangement = Arrangement.SpaceEvenly,
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    items.forEach { item ->
                        IconButton(
                            onClick = { if (currentRoute != item.route) navController.navigate(item.route) },
                            modifier = Modifier.size(36.dp)
                        ) {
                            Icon(
                                painter = painterResource(id = item.iconRes),
                                contentDescription = item.title,
                                tint = if (currentRoute == item.route) MaterialTheme.colorScheme.primary
                                else MaterialTheme.colorScheme.onSurfaceVariant,
                                modifier = Modifier.size(20.dp)
                            )
                        }
                    }
                }

                // Battery (10%)
                Box(modifier = Modifier.weight(0.07f), contentAlignment = Alignment.Center) {
                    BatteryStatusItem(
                        level = batteryLevel,
                        thresholds = listOf(20, 80),
                        icons = listOf(
                            R.drawable.ic_battery_low,
                            R.drawable.ic_battery_medium,
                            R.drawable.ic_battery_full
                        ),
                        modifier = Modifier.size(32.dp)
                    )
                }

                // Logout (10)
                Box(modifier = Modifier.weight(0.03f), contentAlignment = Alignment.CenterEnd) {
                    IconButton(onClick = onLogout, modifier = Modifier.size(36.dp)) {
                        Icon(
                            imageVector = Icons.Default.ExitToApp,
                            contentDescription = "Logout",
                            modifier = Modifier.size(20.dp),
                            tint = MaterialTheme.colorScheme.error
                        )
                    }
                }
            }
        }
    }
}

// ========== PREVIEW SECTION ==========
@Preview(showBackground = true, widthDp = 1080, heightDp = 100)
@Composable
fun TopNavigationBarConnectedPreview() {
    TopNavigationBar(
        navController = rememberNavController(),
        statusValue = 1,
        batteryLevel = 75,
        deviceName = "PLC-01",
        connectionState = ControlViewModel.ConnectionState.Connected,
        onLogout = {}
    )
}

@Preview(showBackground = true, widthDp = 1080, heightDp = 100)
@Composable
fun TopNavigationBarConnectingPreview() {
    TopNavigationBar(
        navController = rememberNavController(),
        statusValue = 2,
        batteryLevel = 50,
        deviceName = "PLC-02",
        connectionState = ControlViewModel.ConnectionState.Connecting(attempt = 2),
        onLogout = {}
    )
}

@Preview(showBackground = true, widthDp = 1080, heightDp = 100)
@Composable
fun TopNavigationBarFailedPreview() {
    TopNavigationBar(
        navController = rememberNavController(),
        statusValue = 11,
        batteryLevel = 10,
        deviceName = "PLC-Error",
        connectionState = ControlViewModel.ConnectionState.Failed(
            error = "Timeout",
            attempt = 3
        ),
        onLogout = {}
    )
}

@Preview(showBackground = true, widthDp = 1080, heightDp = 100)
@Composable
fun TopNavigationBarOfflinePreview() {
    TopNavigationBar(
        navController = rememberNavController(),
        statusValue = 0,
        batteryLevel = 100,
        deviceName = "PLC-Offline",
        connectionState = ControlViewModel.ConnectionState.Offline,
        onLogout = {}
    )
}