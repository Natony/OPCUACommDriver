package com.example.s7opcuaapp.ui.screen.control

import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.material.icons.*
import androidx.compose.material.icons.filled.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import android.util.Log
import com.example.s7opcuaapp.data.model.PlcData
import com.example.s7opcuaapp.ui.components.*
import com.example.s7opcuaapp.viewmodel.ControlViewModel
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch

@Composable
fun ControlScreen(
    uiState: ControlUiState,
    connectionState: ControlViewModel.ConnectionState,
    onNavigateToConfig: () -> Unit,
    onRetryConnection: () -> Unit,
    onToggleBoolean: (Int, Boolean) -> Unit,
    onOpenDialog: (String, Int) -> Unit,
    onConfirmNumber: (Int, Int) -> Unit,
    onDismissDialog: () -> Unit,
    onFunctionSelect: (Int) -> Unit,
    onTextChange: (Int, String) -> Unit,
    onSendAll: () -> Unit,
    onPressButton: (Int) -> Boolean,
    onReleaseButton: (Int) -> Boolean,
    onDismissTimeoutDialog: () -> Unit = {},
    onContinueOffline: () -> Unit
) {
    val data = uiState.plcData
    var isAuto by remember { mutableStateOf(true) }

    // Connection timeout dialog state
    var showTimeoutDialog by remember { mutableStateOf(false) }
    var timeoutCountdown by remember { mutableStateOf(30) }
    var countdownJob by remember { mutableStateOf<Job?>(null) }

    // Monitor connection state for timeout handling
    LaunchedEffect(connectionState) {
        when (connectionState) {
            is ControlViewModel.ConnectionState.MaxRetriesExceeded -> {
                Log.d("ControlScreen", "Max retries exceeded - showing timeout dialog")
                countdownJob?.cancel()
                showTimeoutDialog = true
                timeoutCountdown = 30

                countdownJob = launch {
                    try {
                        while (timeoutCountdown > 0 && showTimeoutDialog) {
                            delay(1000)
                            timeoutCountdown--
                            if (timeoutCountdown % 5 == 0) {
                                Log.d("ControlScreen", "Timeout countdown: ${timeoutCountdown}s")
                            }
                        }
                        if (showTimeoutDialog && timeoutCountdown == 0) {
                            Log.d("ControlScreen", "Timeout expired, navigating to config")
                            showTimeoutDialog = false
                            onNavigateToConfig()
                        }
                    } catch (e: CancellationException) {
                        Log.d("ControlScreen", "Countdown cancelled")
                    }
                }
            }

            is ControlViewModel.ConnectionState.Connected -> {
                if (showTimeoutDialog) {
                    Log.d("ControlScreen", "Connection restored, dismissing timeout dialog")
                    showTimeoutDialog = false
                    countdownJob?.cancel()
                    countdownJob = null
                }
            }
            else -> { /* Don't auto-dismiss for other states */ }
        }
    }

    DisposableEffect(Unit) {
        onDispose {
            countdownJob?.cancel()
        }
    }

    Box(modifier = Modifier.fillMaxSize()) {
        // Main content always visible
        MainControlContent(
            uiState = uiState,
            isAuto = isAuto,
            onToggleAutoMode = { isAuto = !isAuto },
            onToggleBoolean = onToggleBoolean,
            onOpenDialog = onOpenDialog,
            onConfirmNumber = onConfirmNumber,
            onDismissDialog = onDismissDialog,
            onFunctionSelect = onFunctionSelect,
            onTextChange = onTextChange,
            onSendAll = onSendAll,
            onPressButton = onPressButton,
            onReleaseButton = onReleaseButton,
            onRetryConnection = onRetryConnection
        )

        // Connection state overlays
        when (connectionState) {
            is ControlViewModel.ConnectionState.Offline -> {
                OfflineOverlay(
                    onRetryConnection = onRetryConnection,
                    onExitOffline = onNavigateToConfig
                )
            }

            is ControlViewModel.ConnectionState.Connecting -> {
                if (uiState.loadingPercent in 1..99) {
                    LoadingOverlay(
                        message = "Connecting to PLC... (Attempt ${connectionState.attempt}/3)",
                        loadingPercent = uiState.loadingPercent,
                        isIndeterminate = false
                    )
                }
            }

            is ControlViewModel.ConnectionState.Failed -> {
                if (connectionState.error.contains("Connection lost", ignoreCase = true)) {
                    ConnectionLostNotification(onRetryConnection = onRetryConnection)
                }
            }

            else -> { /* No overlay */ }
        }

        // Timeout dialog with improved UX
        if (showTimeoutDialog) {
            AlertDialog(
                onDismissRequest = { /* Can't dismiss by tapping outside */ },
                icon = {
                    Icon(
                        imageVector = Icons.Default.WifiOff,
                        contentDescription = null,
                        tint = MaterialTheme.colorScheme.error,
                        modifier = Modifier.size(48.dp)
                    )
                },
                title = {
                    Text(
                        "Connection Failed",
                        style = MaterialTheme.typography.headlineSmall
                    )
                },
                text = {
                    Column(
                        verticalArrangement = Arrangement.spacedBy(12.dp)
                    ) {
                        Text(
                            "Unable to connect to PLC after 3 attempts.",
                            style = MaterialTheme.typography.bodyLarge
                        )

                        Row(
                            verticalAlignment = Alignment.CenterVertically,
                            horizontalArrangement = Arrangement.spacedBy(8.dp)
                        ) {
                            CircularProgressIndicator(
                                progress = timeoutCountdown / 30f,
                                modifier = Modifier.size(24.dp),
                                strokeWidth = 3.dp
                            )
                            Text(
                                text = if (timeoutCountdown > 0) {
                                    "Auto-returning to config in ${timeoutCountdown}s..."
                                } else {
                                    "Returning to config..."
                                },
                                style = MaterialTheme.typography.bodyMedium,
                                color = MaterialTheme.colorScheme.onSurfaceVariant
                            )
                        }

                        Divider()

                        Text(
                            "What would you like to do?",
                            style = MaterialTheme.typography.labelLarge,
                            fontWeight = FontWeight.SemiBold
                        )

                        Column(
                            verticalArrangement = Arrangement.spacedBy(4.dp)
                        ) {
                            Text("• Retry: Try connecting again", style = MaterialTheme.typography.bodySmall)
                            Text("• Config: Change connection settings", style = MaterialTheme.typography.bodySmall)
                            Text("• Offline: View interface without PLC", style = MaterialTheme.typography.bodySmall)
                        }
                    }
                },
                confirmButton = {
                    Row(
                        horizontalArrangement = Arrangement.spacedBy(8.dp)
                    ) {
                        Button(
                            onClick = {
                                Log.d("ControlScreen", "User chose to retry connection")
                                showTimeoutDialog = false
                                countdownJob?.cancel()
                                countdownJob = null
                                onDismissTimeoutDialog()
                                onRetryConnection()
                            },
                            colors = ButtonDefaults.buttonColors(
                                containerColor = MaterialTheme.colorScheme.primary
                            )
                        ) {
                            Icon(
                                imageVector = Icons.Default.Refresh,
                                contentDescription = null,
                                modifier = Modifier.size(18.dp)
                            )
                            Spacer(modifier = Modifier.width(4.dp))
                            Text("Retry")
                        }

                        OutlinedButton(
                            onClick = {
                                Log.d("ControlScreen", "User chose to go to config")
                                showTimeoutDialog = false
                                countdownJob?.cancel()
                                countdownJob = null
                                onNavigateToConfig()
                            }
                        ) {
                            Icon(
                                imageVector = Icons.Default.Settings,
                                contentDescription = null,
                                modifier = Modifier.size(18.dp)
                            )
                            Spacer(modifier = Modifier.width(4.dp))
                            Text("Config")
                        }
                    }
                },
                dismissButton = {
                    TextButton(
                        onClick = {
                            Log.d("ControlScreen", "User chose offline mode")
                            showTimeoutDialog = false
                            countdownJob?.cancel()
                            countdownJob = null
                            onDismissTimeoutDialog()
                            onContinueOffline()
                        }
                    ) {
                        Text("Continue Offline")
                    }
                }
            )
        }
    }
}

// ========== PREVIEW SECTION ==========
@Preview(showBackground = true, widthDp = 1080, heightDp = 720)
@Composable
fun ControlScreenPreview() {
    val samplePlcData = PlcData(
        bools = List(15) { it % 2 == 0 },
        ints = List(31) { it }
    )

    val sampleState = ControlUiState(
        plcData = samplePlcData,
        isWriting = false,
        openDialogForIndex = null,
        selectedFunction = 1,
        intInputs = mapOf(
            2 to "10", 3 to "20", 4 to "30",
            5 to "40", 6 to "50", 7 to "60"
        ),
        loadingPercent = 100,
        lockedButtons = emptySet(),
        busyButtons = emptySet(),
        isProcessing = false,
        errorMessage = null
    )

    ControlScreen(
        uiState = sampleState,
        connectionState = ControlViewModel.ConnectionState.Connected,
        onNavigateToConfig = {},
        onRetryConnection = {},
        onToggleBoolean = { _, _ -> },
        onOpenDialog = { _, _ -> },
        onConfirmNumber = { _, _ -> },
        onDismissDialog = {},
        onFunctionSelect = {},
        onTextChange = { _, _ -> },
        onSendAll = {},
        onPressButton = { _ -> false },
        onReleaseButton = { _ -> false },
        onDismissTimeoutDialog = {},
        onContinueOffline = {}
    )
}

@Preview(showBackground = true, widthDp = 1080, heightDp = 720)
@Composable
fun ControlScreenOfflinePreview() {
    val sampleState = ControlUiState(
        plcData = PlcData(
            bools = List(15) { false },
            ints = List(31) { 0 }
        ),
        errorMessage = "Working offline",
        lockedButtons = (0..14).toSet(),
        loadingPercent = 100
    )

    ControlScreen(
        uiState = sampleState,
        connectionState = ControlViewModel.ConnectionState.Offline,
        onNavigateToConfig = {},
        onRetryConnection = {},
        onToggleBoolean = { _, _ -> },
        onOpenDialog = { _, _ -> },
        onConfirmNumber = { _, _ -> },
        onDismissDialog = {},
        onFunctionSelect = {},
        onTextChange = { _, _ -> },
        onSendAll = {},
        onPressButton = { _ -> false },
        onReleaseButton = { _ -> false },
        onDismissTimeoutDialog = {},
        onContinueOffline = {}
    )
}