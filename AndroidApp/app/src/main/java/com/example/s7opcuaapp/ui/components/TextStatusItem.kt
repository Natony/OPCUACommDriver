package com.example.s7opcuaapp.ui.components

import androidx.compose.foundation.border
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

/**
 * TextStatusItem hiển thị trạng thái text tương ứng với giá trị int.
 * Không thay đổi các class cũ.
 *
 * @param label Chú thích trên cùng (có thể để trống).
 * @param intValue Giá trị số dùng để chọn trạng thái.
 * @param statuses Danh sách chuỗi, index tương ứng với intValue (quá lớn thì dùng cuối).
 * @param modifier Modifier tuỳ chỉnh.
 */
@Composable
fun TextStatusItem(
    label: String,
    intValue: Int,
    statuses: List<String>,
    modifier: Modifier = Modifier
) {
    // Xác định index trong khoảng cho statuses
    val idx = intValue.coerceIn(0, statuses.size - 1)
    Column(
        modifier = modifier
            .wrapContentHeight()
            .padding(vertical = 4.dp),
        horizontalAlignment = Alignment.Start
    ) {
        if (label.isNotEmpty()) {
            Text(
                text = label,
                fontSize = 14.sp,
                modifier = Modifier.padding(bottom = 2.dp)
            )
        }
        Box(
            modifier = Modifier
                .wrapContentWidth()
                .height(56.dp)
                .border(
                    width = 1.dp,
                    color = MaterialTheme.colorScheme.onSurface.copy(alpha = 0.5f),
                    shape = RoundedCornerShape(4.dp)
                )
                .clip(RoundedCornerShape(4.dp))
                .padding(horizontal = 8.dp),
            contentAlignment = Alignment.Center
        ) {
            Text(
                text = statuses[idx],
                fontSize = 16.sp,
                color = MaterialTheme.colorScheme.onSurface
            )
        }
    }
}
