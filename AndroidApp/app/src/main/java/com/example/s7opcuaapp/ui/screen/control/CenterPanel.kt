package com.example.s7opcuaapp.ui.screen.control

import androidx.compose.foundation.layout.*
import androidx.compose.material3.Button
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.s7opcuaapp.R
import com.example.s7opcuaapp.ui.components.*
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.ui.text.style.TextAlign

@Composable
fun CenterPanel(
    uiState: ControlUiState,
    isAuto: Boolean,
    onToggleBoolean: (Int, Boolean) -> Unit,
    onFunctionSelect: (Int) -> Unit,
    onTextChange: (Int, String) -> Unit,
    onSendAll: () -> Unit,
    modifier: Modifier = Modifier
) {
    val data = uiState.plcData
    val isWriting = uiState.isWriting

    val SEND_ALL_BUTTON_INDEX = 999
    val isSendAllLocked = SEND_ALL_BUTTON_INDEX in uiState.lockedButtons

    Column(
        modifier = modifier
            .fillMaxHeight()
            .padding(1.dp),
        verticalArrangement = Arrangement.spacedBy(3.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        // Positions row
        Row(
            modifier = Modifier.weight(0.25f),
            horizontalArrangement = Arrangement.spacedBy(1.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            MultiStateStatusItem(
                "Pos1", data.ints.getOrNull(15) ?: 0, listOf(
                    R.drawable.ic_pos1_state0,
                    R.drawable.ic_pos1_state1,
                    R.drawable.ic_pos1_state2
                ), modifier = Modifier.size(36.dp)
            )
            MultiStateStatusItem(
                "Pos2", data.ints.getOrNull(16) ?: 0, listOf(
                    R.drawable.ic_pos2_state0,
                    R.drawable.ic_pos2_state1,
                    R.drawable.ic_pos2_state2
                ), modifier = Modifier.size(36.dp)
            )
            MultiStateStatusItem(
                "Pos3", data.ints.getOrNull(17) ?: 0, listOf(
                    R.drawable.ic_pos3_state0,
                    R.drawable.ic_pos3_state1,
                    R.drawable.ic_pos3_state2
                ), modifier = Modifier.size(36.dp)
            )
            MultiStateStatusItem(
                "Pos4", data.ints.getOrNull(18) ?: 0, listOf(
                    R.drawable.ic_pos4_state0,
                    R.drawable.ic_pos4_state1,
                    R.drawable.ic_pos4_state2
                ), modifier = Modifier.size(36.dp)
            )
            MultiStateStatusItem(
                "Pos5", data.ints.getOrNull(19) ?: 0, listOf(
                    R.drawable.ic_pos5_state0,
                    R.drawable.ic_pos5_state1,
                    R.drawable.ic_pos5_state2
                ), modifier = Modifier.size(36.dp)
            )
            MultiStateStatusItem(
                "Pos6", data.ints.getOrNull(20) ?: 0, listOf(
                    R.drawable.ic_pos6_state0,
                    R.drawable.ic_pos6_state1,
                    R.drawable.ic_pos6_state2
                ), modifier = Modifier.size(36.dp)
            )
            MultiStateStatusItem(
                "Pos7", data.ints.getOrNull(21) ?: 0, listOf(
                    R.drawable.ic_pos7_state0,
                    R.drawable.ic_pos7_state1,
                    R.drawable.ic_pos7_state2
                ), modifier = Modifier.size(36.dp)
            )
            MultiStateStatusItem(
                "Pos8", data.ints.getOrNull(22) ?: 0, listOf(
                    R.drawable.ic_pos8_state0,
                    R.drawable.ic_pos8_state1,
                    R.drawable.ic_pos8_state2
                ), modifier = Modifier.size(36.dp)
            )
            MultiStateStatusItem(
                "Pos9", data.ints.getOrNull(23) ?: 0, listOf(
                    R.drawable.ic_pos9_state0,
                    R.drawable.ic_pos9_state1,
                    R.drawable.ic_pos9_state2
                ), modifier = Modifier.size(36.dp)
            )
            MultiStateStatusItem(
                "Pos10", data.ints.getOrNull(24) ?: 0, listOf(
                    R.drawable.ic_pos10_state0,
                    R.drawable.ic_pos10_state1,
                    R.drawable.ic_pos10_state2
                ), modifier = Modifier.size(36.dp)
            )
            MultiStateStatusItem(
                "Pos11", data.ints.getOrNull(25) ?: 0, listOf(
                    R.drawable.ic_pos11_state0,
                    R.drawable.ic_pos11_state1,
                    R.drawable.ic_pos11_state2
                ), modifier = Modifier.size(36.dp)
            )
            MultiStateStatusItem(
                "Pos12", data.ints.getOrNull(26) ?: 0, listOf(
                    R.drawable.ic_pos12_state0,
                    R.drawable.ic_pos12_state1,
                    R.drawable.ic_pos12_state2
                ), modifier = Modifier.size(36.dp)
            )
            MultiStateStatusItem(
                "Pos13", data.ints.getOrNull(27) ?: 0, listOf(
                    R.drawable.ic_pos13_state0,
                    R.drawable.ic_pos13_state1,
                    R.drawable.ic_pos13_state2
                ), modifier = Modifier.size(36.dp)
            )
        }

        // Start/End/Actual/Function/Send row
        Row(
            modifier = Modifier
                .weight(0.5f)
                .fillMaxWidth(),
            horizontalArrangement = Arrangement.spacedBy(1.dp),
            verticalAlignment = Alignment.CenterVertically
        ) {
            // Start point cluster
            Column(
                modifier = Modifier.weight(1f),
                verticalArrangement = Arrangement.Center,
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                Text("Điểm bắt đầu", style = MaterialTheme.typography.bodySmall)
                Row(
                    horizontalArrangement = Arrangement.spacedBy(2.dp),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    InlineNumberInputItem(
//                        label = "X",
                        textValue = uiState.intInputs[5] ?: (data.ints.getOrNull(5)
                            ?.toString() ?: "0"),
                        isWriting = isWriting,
                        onTextChange = { onTextChange(5, it) },
                        modifier = Modifier.weight(1f)
                    )
                    InlineNumberInputItem(
//                        label = "Y",
                        textValue = uiState.intInputs[6] ?: (data.ints.getOrNull(6)
                            ?.toString() ?: "0"),
                        isWriting = isWriting,
                        onTextChange = { onTextChange(6, it) },
                        modifier = Modifier.weight(1f)
                    )
                    InlineNumberInputItem(
//                        label = "Z",
                        textValue = uiState.intInputs[7] ?: (data.ints.getOrNull(7)
                            ?.toString() ?: "0"),
                        isWriting = isWriting,
                        onTextChange = { onTextChange(7, it) },
                        modifier = Modifier.weight(1f)
                    )
                }
            }
            // End point cluster
            Column(
                modifier = Modifier.weight(1f),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                Text("Điểm kết thúc", style = MaterialTheme.typography.bodySmall)
                Row(
                    horizontalArrangement = Arrangement.spacedBy(2.dp),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    InlineNumberInputItem(
//                        label = "X",
                        textValue = uiState.intInputs[8] ?: (data.ints.getOrNull(8)
                            ?.toString() ?: "0"),
                        isWriting = isWriting,
                        onTextChange = { onTextChange(8, it) },
                        modifier = Modifier.weight(1f)
                    )
                    InlineNumberInputItem(
//                        label = "Y",
                        textValue = uiState.intInputs[9] ?: (data.ints.getOrNull(9)
                            ?.toString() ?: "0"),
                        isWriting = isWriting,
                        onTextChange = { onTextChange(9, it) },
                        modifier = Modifier.weight(1f)
                    )
                    InlineNumberInputItem(
//                        label = "Z",
                        textValue = uiState.intInputs[10] ?: (data.ints.getOrNull(10)
                            ?.toString() ?: "0"),
                        isWriting = isWriting,
                        onTextChange = { onTextChange(10, it) },
                        modifier = Modifier.weight(1f)
                    )
                }
            }
            // Actual point cluster
            Column(
                modifier = Modifier.weight(1f),
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                Text("Thực tế", style = MaterialTheme.typography.bodySmall)
                Row(
                    horizontalArrangement = Arrangement.spacedBy(2.dp),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    NumericStatusItem(
//                        label = "X",
                        value = data.ints.getOrNull(11) ?: 0,
                        modifier = Modifier.weight(1f)
                    )
                    NumericStatusItem(
//                        label = "Y",
                        value = data.ints.getOrNull(12) ?: 0,
                        modifier = Modifier.weight(1f)
                    )
                    NumericStatusItem(
//                        label = "Z",
                        value = data.ints.getOrNull(13) ?: 0,
                        modifier = Modifier.weight(1f)
                    )
                }
            }

            Column(
                modifier = Modifier
                    .weight(1f)
                    .fillMaxHeight(),
                verticalArrangement = Arrangement.Center,
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                // Function selector dropdown placeholder
                Box(modifier = Modifier.weight(1f), contentAlignment = Alignment.Center) {
                    FunctionListSelector(
                        entries = listOf(
                            "Function 1" to 1,
                            "Function 2" to 2,
                            "Function 3" to 3
                        ),
                        selectedCode = uiState.selectedFunction,
                        onSelect = onFunctionSelect,
                        modifier = Modifier.fillMaxWidth()
                    )
                }
            }

            Column(
                modifier = Modifier
                    .weight(1f)
                    .fillMaxHeight(),
                verticalArrangement = Arrangement.Center,
                horizontalAlignment = Alignment.CenterHorizontally
            ) {
                SendAllButton(
                    isAutoMode = isAuto,
                    isWriting = isWriting,
                    isLocked = isSendAllLocked,
                    onClick = onSendAll,
                    modifier = Modifier.fillMaxWidth()
                )
            }
        }
    }
}