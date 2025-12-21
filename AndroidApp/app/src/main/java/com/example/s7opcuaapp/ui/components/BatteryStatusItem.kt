package com.example.s7opcuaapp.ui.components

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.wrapContentHeight
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

/**
 * BatteryStatusItem hiển thị icon pin theo mức pin với ngưỡng tuỳ chỉnh.
 * @param level Giá trị pin thực tế (0..100)
 * @param thresholds Danh sách mức ngưỡng [t0, t1, ..., tN-2], must be sorted, length = icons.size - 1.
 *                   Ví dụ [20, 80] với icons.size=3 nghĩa: level<20 -> icon0, 20<=level<80 -> icon1, >=80 -> icon2.
 * @param icons Danh sách drawable tương ứng các mức pin, icons.size = thresholds.size + 1.
 */
@Composable
fun BatteryStatusItem(
    level: Int,
    thresholds: List<Int>,
    icons: List<Int>,
    modifier: Modifier = Modifier
) {
    // Ensure icons and thresholds lengths match
    val idx = when {
        icons.size != thresholds.size + 1 -> 0
        else -> {
            val clamped = level.coerceIn(0, 100)
            thresholds.indexOfFirst { clamped < it }.let { if (it == -1) thresholds.size else it }
        }
    }

    Row(
        modifier = Modifier
            .fillMaxWidth()
            .padding(5.dp),
        verticalAlignment = Alignment.CenterVertically
    ) {
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .weight(1f),
            contentAlignment = Alignment.Center
        ) {
            Icon(
                painter = painterResource(id = icons[idx]),
                contentDescription = "Battery level $level",
                modifier = Modifier.size(36.dp),
                tint = Color.Unspecified
            )
        }
        Box(
            modifier = Modifier
                .fillMaxWidth()
                .weight(1f),
            contentAlignment = Alignment.Center
        ) {
            Text(
                text = "$level%",
                fontSize = 12.sp,
                color = MaterialTheme.colorScheme.onSurface
            )
        }
    }
}
