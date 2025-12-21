package com.example.s7opcuaapp.ui.screen.control

import androidx.compose.foundation.layout.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.example.s7opcuaapp.R
import com.example.s7opcuaapp.ui.components.BatteryStatusItem
import com.example.s7opcuaapp.ui.components.TextStatusItem
import com.example.s7opcuaapp.util.StatusLockConfig

@Composable
fun TopStatusBar(statusValue: Int, batteryLevel: Int) {
    Row(
        modifier = Modifier
            .fillMaxWidth()
            .height(48.dp)
            .padding(vertical = 3.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Box(
            modifier = Modifier.weight(0.9f),
            contentAlignment = Alignment.Center
        ) {
            TextStatusItem(
                label = "",
                intValue = statusValue,
                statuses = StatusLockConfig.DEFAULT_STATUS_DESCRIPTIONS.values.toList(),
                modifier = Modifier.wrapContentSize()
            )
        }
        Box(
            modifier = Modifier.weight(0.1f),
            contentAlignment = Alignment.CenterEnd
        ) {
            BatteryStatusItem(
                level = batteryLevel,
                thresholds = listOf(20, 80),
                icons = listOf(
                    R.drawable.ic_battery_low,
                    R.drawable.ic_battery_medium,
                    R.drawable.ic_battery_full
                ),
                modifier = Modifier.size(48.dp)
            )
        }
    }
}