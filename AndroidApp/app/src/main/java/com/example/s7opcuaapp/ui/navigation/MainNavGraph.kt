package com.example.s7opcuaapp.ui.navigation

import android.util.Log
import androidx.compose.foundation.layout.*
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Scaffold
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.collectAsState
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.navigation.NavHostController
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.navigation.compose.currentBackStackEntryAsState
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.example.s7opcuaapp.data.local.PrefsManager
import com.example.s7opcuaapp.ui.screen.config.ConfigScreen
import com.example.s7opcuaapp.ui.screen.control.ControlScreen
import com.example.s7opcuaapp.ui.screen.home.HomeScreen
import com.example.s7opcuaapp.ui.screen.history.LoginHistoryScreen
import com.example.s7opcuaapp.ui.screen.usermanager.UserManagerScreen
import com.example.s7opcuaapp.viewmodel.*
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun MainNavGraph(rootNavController: NavHostController) {
    val topNavController = androidx.navigation.compose.rememberNavController()
    val navBackStackEntry by topNavController.currentBackStackEntryAsState()
    val currentRoute = navBackStackEntry?.destination?.route

    val controlViewModel: ControlViewModel = hiltViewModel()
    val controlUiState by controlViewModel.uiState.collectAsStateWithLifecycle()
    val logoutViewModel: LogoutViewModel = hiltViewModel()
    val connectionState by controlViewModel.connectionState.collectAsStateWithLifecycle()

    // Get PrefsManager directly
    val context = LocalContext.current
    val prefsManager = remember { PrefsManager(context) }
    val currentDevice = remember { mutableStateOf(prefsManager.getCurrentDevice()) }

    // Add coroutine scope for proper async handling
    val coroutineScope = rememberCoroutineScope()

    var shouldReconnect by remember { mutableStateOf(false) }
    var lastRoute by remember { mutableStateOf<String?>(null) }

    // Monitor navigation and connection state
    LaunchedEffect(currentRoute) {
        Log.d("MainNavGraph", "Route changed to: $currentRoute")

        when (currentRoute) {
            "control" -> {
                // Update current device
                currentDevice.value = prefsManager.getCurrentDevice()

                // Always refresh connection state when returning to control
                controlViewModel.refreshConnectionState()

                // Start connection if needed
                when (connectionState) {
                    is ControlViewModel.ConnectionState.Idle -> {
                        Log.d("MainNavGraph", "Control screen - Idle state, starting connection")
                        controlViewModel.startConnection()
                    }
                    is ControlViewModel.ConnectionState.Failed -> {
                        // Only auto-reconnect if we just navigated back
                        if (lastRoute == "config_btm" && shouldReconnect) {
                            Log.d("MainNavGraph", "Returning from config with reconnect flag")
                            shouldReconnect = false
                            controlViewModel.resetConnection()
                        }
                    }
                    is ControlViewModel.ConnectionState.Connected -> {
                        Log.d("MainNavGraph", "Already connected, refreshing state")
                        // Just refresh to ensure UI is in sync
                        controlViewModel.refreshConnectionState()
                    }
                    else -> {
                        Log.d("MainNavGraph", "Control screen - State: $connectionState")
                    }
                }
            }

            "config_btm" -> {
                // Don't stop connection when going to config
                Log.d("MainNavGraph", "Entered config, maintaining connection state")
            }
        }

        lastRoute = currentRoute
    }

    // Handle connection state changes globally
    LaunchedEffect(connectionState) {
        val currentConnectionState = connectionState // Capture for smart cast
        when (currentConnectionState) {
            is ControlViewModel.ConnectionState.MaxRetriesExceeded -> {
                Log.e("MainNavGraph", "Max retries exceeded: ${currentConnectionState.reason}")
                // Could show a global error dialog or navigate to config
                if (currentRoute == "control") {
                    delay(2000) // Give user time to see the error
                    topNavController.navigate("config_btm")
                }
            }
            is ControlViewModel.ConnectionState.Timeout -> {
                Log.e("MainNavGraph", "Connection timeout")
                // Let ControlScreen handle timeout navigation
            }
            is ControlViewModel.ConnectionState.Failed -> {
                Log.e("MainNavGraph", "Connection failed: ${currentConnectionState.error}")
                // Let ControlScreen handle failures
            }
            else -> {
                // Other states handled by respective screens
            }
        }
    }

    Scaffold(
        topBar = {
            TopNavigationBar(
                navController = topNavController,
                statusValue = controlUiState.plcData.ints.getOrNull(0) ?: 0,
                batteryLevel = controlUiState.plcData.ints.getOrNull(1) ?: 100,
                deviceName = currentDevice.value?.name ?: "No Device",
                connectionState = connectionState,
                onLogout = {
                    logoutViewModel.logout {
                        rootNavController.navigate("login") {
                            popUpTo(0) { inclusive = true }
                        }
                    }
                }
            )
        }
    ) { paddingValues ->
        NavHost(
            navController = topNavController,
            startDestination = "control",
            modifier = Modifier.padding(paddingValues)
        ) {
            composable("control") {
                ControlScreen(
                    uiState = controlUiState,
                    connectionState = connectionState,
                    onNavigateToConfig = {
                        topNavController.navigate("config_btm")
                    },
                    onRetryConnection = {
                        Log.d("MainNavGraph", "User requested retry connection")
                        // Full reset and retry
                        controlViewModel.resetConnection()
                    },
                    onToggleBoolean = { idx, newVal ->
                        controlViewModel.onToggleBoolean(idx, newVal)
                    },
                    onOpenDialog = { title, index ->
                        controlViewModel.openNumberDialog(title, index)
                    },
                    onConfirmNumber = { index, value ->
                        controlViewModel.confirmNumber(index, value)
                    },
                    onDismissDialog = {
                        controlViewModel.dismissDialog()
                    },
                    onFunctionSelect = { code ->
                        controlViewModel.onFunctionSelected(code)
                    },
                    onTextChange = { idx, txt ->
                        controlViewModel.onInlineValueChange(idx, txt)
                    },
                    onSendAll = {
                        controlViewModel.onSendAll()
                    },
                    onPressButton = { index ->
                        controlViewModel.onPressButton(index)
                        true
                    },
                    onReleaseButton = { index ->
                        controlViewModel.onReleaseButton(index)
                        true
                    },
                    onDismissTimeoutDialog = {
                        controlViewModel.dismissTimeoutDialog()
                    },
                    onContinueOffline = {
                        controlViewModel.continueOffline()
                    }
                )
            }

            composable("home") {
                val homeViewModel: HomeViewModel = hiltViewModel()
                HomeScreen(navController = topNavController)
            }

            composable("user_manager") {
                val userManagerViewModel: UserManagerViewModel = hiltViewModel()
                UserManagerScreen(viewModel = userManagerViewModel)
            }

            composable("login_history") {
                val loginHistoryViewModel: LoginHistoryViewModel = hiltViewModel()
                LoginHistoryScreen(viewModel = loginHistoryViewModel)
            }

            composable("status_lock_config") {
                val statusLockConfigViewModel: StatusLockConfigViewModel = hiltViewModel()
                com.example.s7opcuaapp.ui.screen.admin.StatusLockConfigScreen(
                    viewModel = statusLockConfigViewModel,
                    onBack = { topNavController.popBackStack() }
                )
            }

            composable("config_btm") {
                val configViewModel: ConfigViewModel = hiltViewModel()
                val uiState by configViewModel.uiState.collectAsStateWithLifecycle()

                ConfigScreen(
                    uiState = uiState,
                    connectionState = connectionState,
                    onNewDeviceNameChanged = { deviceName ->
                        configViewModel.onNewDeviceNameChanged(deviceName)
                    },
                    onNewDeviceIpChanged = { deviceIp ->
                        configViewModel.onNewDeviceIpChanged(deviceIp)
                    },
                    onNewDevicePortChanged = { devicePort ->
                        configViewModel.onNewDevicePortChanged(devicePort)
                    },
                    onNewDeviceUsernameChanged = { deviceUsername ->
                        configViewModel.onNewDeviceUsernameChanged(deviceUsername)
                    },
                    onNewDevicePasswordChanged = { devicePassword ->
                        configViewModel.onNewDevicePasswordChanged(devicePassword)
                    },
                    onAddDevice = { configViewModel.onAddDevice() },
                    onRemoveDevice = { device -> configViewModel.onRemoveDevice(device) },
                    onSelectDevice = { device ->
                        configViewModel.onSelectDevice(device) {
                            coroutineScope.launch {
                                Log.d("MainNavGraph", "📱 Device selected: ${device.name}")

                                // Update current device
                                currentDevice.value = device

                                // Complete reset when changing device
                                Log.d("MainNavGraph", "🛑 Stopping current connection...")
                                controlViewModel.stopConnection()

                                // Wait for full cleanup
                                delay(2000)

                                // Reset all states
                                Log.d("MainNavGraph", "♻️ Resetting connection states...")
                                controlViewModel.resetConnectionAttempts()

                                // Set flag to reconnect
                                shouldReconnect = true

                                // Navigate back to control
                                Log.d("MainNavGraph", "📍 Navigating to control screen...")
                                topNavController.navigate("control") {
                                    popUpTo("control") { inclusive = true }
                                    launchSingleTop = true
                                }
                            }
                        }
                    },
                    onEditDevice = { device -> configViewModel.onEditDevice(device) },
                    onCancelEdit = { configViewModel.onCancelEdit() }
                )
            }
        }
    }

    DisposableEffect(Unit) {
        onDispose {
            controlViewModel.stopConnection()
        }
    }
}