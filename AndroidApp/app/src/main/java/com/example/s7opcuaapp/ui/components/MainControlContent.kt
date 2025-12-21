package com.example.s7opcuaapp.ui.components

import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.*
import androidx.compose.ui.platform.LocalInspectionMode
import androidx.compose.ui.unit.dp
import com.example.s7opcuaapp.BuildConfig
import com.example.s7opcuaapp.ui.screen.control.BottomControlsRow
import com.example.s7opcuaapp.ui.screen.control.CenterPanel
import com.example.s7opcuaapp.ui.screen.control.ControlUiState
import com.example.s7opcuaapp.ui.screen.control.LeftControlPanel
import com.example.s7opcuaapp.ui.screen.control.RightControlPanel

@Composable
internal fun MainControlContent(
    uiState: ControlUiState,
    isAuto: Boolean,
    onToggleAutoMode: () -> Unit,
    onToggleBoolean: (Int, Boolean) -> Unit,
    onOpenDialog: (String, Int) -> Unit,
    onConfirmNumber: (Int, Int) -> Unit,
    onDismissDialog: () -> Unit,
    onFunctionSelect: (Int) -> Unit,
    onTextChange: (Int, String) -> Unit,
    onSendAll: () -> Unit,
    onPressButton: (Int) -> Boolean,
    onReleaseButton: (Int) -> Boolean,
    onRetryConnection: () -> Unit
) {
    SingleTouchHandler(modifier = Modifier.fillMaxSize()) {
        // Dialog
        uiState.openDialogForIndex?.let { idx ->
            NumberInputDialog(
                title = uiState.dialogTitle.ifBlank { "Nhập giá trị" },
                initialValue = uiState.plcData.ints.getOrNull(idx)?.toString() ?: "0",
                onConfirm = { value -> onConfirmNumber(idx, value) },
                onDismiss = onDismissDialog
            )
        }

        Column(modifier = Modifier.fillMaxSize()) {
            // Connection lost notification at top
            if (uiState.errorMessage?.contains("Connection lost", ignoreCase = true) == true ||
                uiState.errorMessage?.contains("Reconnecting", ignoreCase = true) == true
            ) {
                ConnectionLostNotification(onRetryConnection = onRetryConnection)
            }

            // Main control panels
            Row(
                modifier = Modifier
                    .fillMaxSize()
                    .padding(2.dp)
            ) {
                // Left Panel
                LeftControlPanel(
                    isAuto = isAuto,
                    data = uiState.plcData,
                    onToggleBoolean = onToggleBoolean,
                    onOpenDialog = onOpenDialog,
                    onPressButton = onPressButton,
                    onReleaseButton = onReleaseButton,
                    lockedButtons = uiState.lockedButtons,
                    busyButtons = uiState.busyButtons,
                    modifier = Modifier.weight(0.2f)
                )

                // Center Section
                CenterSection(
                    uiState = uiState,
                    isAuto = isAuto,
                    onToggleBoolean = onToggleBoolean,
                    onToggleAutoMode = onToggleAutoMode,
                    onFunctionSelect = onFunctionSelect,
                    onTextChange = onTextChange,
                    onSendAll = onSendAll,
                    modifier = Modifier.weight(0.6f)
                )

                // Right Panel
                RightControlPanel(
                    isAuto = isAuto,
                    data = uiState.plcData,
                    onToggleBoolean = onToggleBoolean,
                    onOpenDialog = onOpenDialog,
                    onPressButton = onPressButton,
                    onReleaseButton = onReleaseButton,
                    lockedButtons = uiState.lockedButtons,
                    busyButtons = uiState.busyButtons,
                    modifier = Modifier.weight(0.2f)
                )
            }
        }
        // Connection lost notification
        if (uiState.errorMessage?.contains("Connection lost") == true) {
            ConnectionLostNotification(onRetryConnection = onRetryConnection)
        }

        // Performance overlay in debug
//        val isInPreview = LocalInspectionMode.current
//        if (BuildConfig.DEBUG && !isInPreview) {
//            PerformanceOverlay(modifier = Modifier.padding(16.dp))
//        }
    }
}

@Composable
private fun CenterSection(
    uiState: ControlUiState,
    isAuto: Boolean,
    onToggleBoolean: (Int, Boolean) -> Unit,
    onToggleAutoMode: () -> Unit,
    onFunctionSelect: (Int) -> Unit,
    onTextChange: (Int, String) -> Unit,
    onSendAll: () -> Unit,
    modifier: Modifier = Modifier
) {
    Column(
        modifier = modifier
            .fillMaxSize()
            .border(
                width = 1.dp,
                color = MaterialTheme.colorScheme.primary,
                shape = RoundedCornerShape(8.dp)
            )
    ) {
        // Top Section
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .weight(1f)
                .padding(2.dp)
        ) {
            CenterPanel(
                uiState = uiState,
                isAuto = isAuto,
                onToggleBoolean = onToggleBoolean,
                onFunctionSelect = onFunctionSelect,
                onTextChange = onTextChange,
                onSendAll = onSendAll,
                modifier = Modifier.weight(0.3f)
            )
        }

        // Bottom Section
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .weight(1f)
                .padding(2.dp)
        ) {
            BottomControlsRow(
                isAuto = isAuto,
                data = uiState.plcData,
                onToggleBoolean = onToggleBoolean,
                onToggleAutoMode = onToggleAutoMode,
                modifier = Modifier.weight(0.7f),
                lockedButtons = uiState.lockedButtons,
                busyButtons = uiState.busyButtons,
                isProcessing = uiState.isProcessing
            )
        }
    }
}