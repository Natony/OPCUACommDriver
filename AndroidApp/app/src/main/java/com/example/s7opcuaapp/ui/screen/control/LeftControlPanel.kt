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
fun LeftControlPanel(
    isAuto: Boolean,
    data: PlcData,
    onToggleBoolean: (Int, Boolean) -> Unit,
    onOpenDialog: (String, Int) -> Unit,
    onPressButton: (Int) -> Boolean,  // Updated signature
    onReleaseButton: (Int) -> Boolean, // Updated signature
    lockedButtons: Set<Int>,
    busyButtons: Set<Int>,
    modifier: Modifier = Modifier
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
            // Auto mode controls - using regular BoolControlItem
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .weight(1f),
                contentAlignment = Alignment.Center
            ) {
                IntControlItem(
                    intValue = data.ints.getOrNull(4) ?: 0,
                    icons = listOf(
                        R.drawable.ic_pallets_minus_off,
                        R.drawable.ic_pallets_minus_on
                    ),
                    onClick = { onOpenDialog("Nhập số lượng lấy pallet ra",4) },
                    enabled = (4 + 200) !in lockedButtons,
                    isProcessing = (4 + 200) in busyButtons
                )
            }

            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .weight(1f),
                contentAlignment = Alignment.Center
            ) {
                BoolControlItem(
                    value = data.bools.getOrNull(6) ?: false,
                    iconOn = R.drawable.ic_pallet_minus_on,
                    iconOff = R.drawable.ic_pallet_minus_off,
                    onClick = { onToggleBoolean(6, data.bools.getOrNull(6)?.not() ?: false) },
                    enabled = 6 !in lockedButtons,
                    isProcessing = 6 in busyButtons
                )
            }

            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .weight(1f),
                contentAlignment = Alignment.Center
            ) {
                BoolControlItem(
                    value = data.bools.getOrNull(8) ?: false,
                    iconOn = R.drawable.ic_stack_pallets_a_on,
                    iconOff = R.drawable.ic_stack_pallets_a_off,
                    onClick = { onToggleBoolean(8, data.bools.getOrNull(8)?.not() ?: false) },
                    enabled = 8 !in lockedButtons,
                    isProcessing = 8 in busyButtons
                )
            }
        } else {
            // Manual mode controls - using PressReleaseBoolControlItem for movement
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .weight(1f),
                contentAlignment = Alignment.Center
            ) {
                PressReleaseBoolControlItem(
                    value = data.bools.getOrNull(0) ?: false,
                    iconOn = R.drawable.ic_shuttle_forward_on,
                    iconOff = R.drawable.ic_shuttle_forward_off,
                    onPress = { onPressButton(0) },
                    onRelease = { onReleaseButton(0) },
                    enabled = 0 !in lockedButtons
                )
            }

            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .weight(1f),
                contentAlignment = Alignment.Center
            ) {
                PressReleaseBoolControlItem(
                    value = data.bools.getOrNull(1) ?: false,
                    iconOn = R.drawable.ic_shuttle_reverse_on,
                    iconOff = R.drawable.ic_shuttle_reverse_off,
                    onPress = { onPressButton(1) },
                    onRelease = { onReleaseButton(1) },
                    enabled = 1 !in lockedButtons
                )
            }
        }
    }
}