package com.example.s7opcuaapp.ui.screen.control

import com.example.s7opcuaapp.data.model.PlcData

data class ControlUiState(
    val plcData: PlcData = PlcData.empty(),
    val isWriting: Boolean = false,
    val openDialogForIndex: Int? = null,
    val dialogTitle: String = "",
    val errorMessage: String? = null,
    val selectedFunction: Int = 0,
    val intInputs: Map<Int, String> = emptyMap(),
    val loadingPercent: Int = 0,
    val lockedButtons: Set<Int> = emptySet(),
    val busyButtons: Set<Int> = emptySet(),
    val isProcessing: Boolean = false,
    val controlsBlockedByAlarm: Boolean = false
)