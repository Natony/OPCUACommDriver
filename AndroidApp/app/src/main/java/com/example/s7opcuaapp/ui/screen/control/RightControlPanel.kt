package com.example.s7opcuaapp.ui.screen.control

import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.example.s7opcuaapp.data.model.PlcData
import com.example.s7opcuaapp.R
import com.example.s7opcuaapp.ui.components.BoolControlItem
import com.example.s7opcuaapp.ui.components.IntControlItem
import com.example.s7opcuaapp.ui.components.PressReleaseBoolControlItem

@Composable
fun RightControlPanel(
    isAuto: Boolean,
    data: PlcData,
    onToggleBoolean: (Int, Boolean) -> Unit,
    onOpenDialog: (String, Int) -> Unit,
    onPressButton: (Int) -> Boolean,  // Added
    onReleaseButton: (Int) -> Boolean, // Added
    modifier: Modifier = Modifier,
    lockedButtons: Set<Int>,
    busyButtons: Set<Int>
) {
    Column(
        modifier = modifier
            .fillMaxHeight()
            .border(
                width = 1.dp,
                color = MaterialTheme.colorScheme.primary,
                shape = RoundedCornerShape(8.dp)
            ),
        verticalArrangement = Arrangement.spacedBy(12.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        if (isAuto) {
            // Auto mode - regular controls
            Box(
                modifier = Modifier.fillMaxWidth().weight(1f),
                contentAlignment = Alignment.Center
            ) {
                IntControlItem(
                    intValue = data.ints.getOrNull(3) ?: 0,
                    icons = listOf(
                        R.drawable.ic_pallets_plus_off,
                        R.drawable.ic_pallets_plus_on
                    ),
                    onClick = { onOpenDialog("Nhập số lượng đưa pallet vào",3) },
                    enabled = (3 + 200) !in lockedButtons,
                    isProcessing = (3 + 200) in busyButtons
                )
            }
            Box(
                modifier = Modifier.fillMaxWidth().weight(1f),
                contentAlignment = Alignment.Center
            ) {
                BoolControlItem(
                    value = data.bools.getOrNull(7) ?: false,
                    iconOn = R.drawable.ic_pallet_plus_on,
                    iconOff = R.drawable.ic_pallet_plus_off,
                    onClick = { onToggleBoolean(7, data.bools.getOrNull(7)?.not() ?: false) },
                    enabled = 7 !in lockedButtons,
                    isProcessing = 7 in busyButtons
                )
            }
            Box(
                modifier = Modifier.fillMaxWidth().weight(1f),
                contentAlignment = Alignment.Center
            ) {
                BoolControlItem(
                    value = data.bools.getOrNull(9) ?: false,
                    iconOn = R.drawable.ic_stack_pallets_b_on,
                    iconOff = R.drawable.ic_stack_pallets_b_off,
                    onClick = { onToggleBoolean(9, data.bools.getOrNull(9)?.not() ?: false) },
                    enabled = 9 !in lockedButtons,
                    isProcessing = 9 in busyButtons
                )
            }
        } else {
            // Manual mode - use press/release for up/down movement
            Box(
                modifier = Modifier.fillMaxWidth().weight(1f),
                contentAlignment = Alignment.Center
            ) {
                PressReleaseBoolControlItem(
                    value = data.bools.getOrNull(2) ?: false,
                    iconOn = R.drawable.ic_shuttle_up_on,
                    iconOff = R.drawable.ic_shuttle_up_off,
                    onPress = { onPressButton(2) },
                    onRelease = { onReleaseButton(2) },
                    enabled = 2 !in lockedButtons
                )
            }
            Box(
                modifier = Modifier.fillMaxWidth().weight(1f),
                contentAlignment = Alignment.Center
            ) {
                PressReleaseBoolControlItem(
                    value = data.bools.getOrNull(3) ?: false,
                    iconOn = R.drawable.ic_shuttle_down_on,
                    iconOff = R.drawable.ic_shuttle_down_off,
                    onPress = { onPressButton(3) },
                    onRelease = { onReleaseButton(3) },
                    enabled = 3 !in lockedButtons
                )
            }
        }
    }
}