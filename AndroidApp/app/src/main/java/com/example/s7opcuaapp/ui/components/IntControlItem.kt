package com.example.s7opcuaapp.ui.components

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.material3.Icon
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.Alignment
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.graphics.Color

/**
 * Composable hiển thị icon và giá trị số (Int) với khả năng click,
 * không có nền.
 *
 * @param intValue   Giá trị số cần hiển thị
 * @param icons      Danh sách icon tương ứng với các giá trị trạng thái
 * @param onClick    Callback khi nhấn vào item
 * @param enabled    Cho phép nhấn
 * @param isProcessing Hiển thị trạng thái đang xử lý
 */
@Composable
fun IntControlItem(
    intValue: Int,
    icons: List<Int>,
    onClick: () -> Unit,
    enabled: Boolean = true,
    isProcessing: Boolean = false,
    modifier: Modifier = Modifier
) {
    val iconRes = icons.getOrElse(intValue) { icons.last() }

    Column(
        modifier = modifier
            .wrapContentSize()
            .clickable(enabled = enabled && !isProcessing) { onClick() },
        horizontalAlignment = Alignment.CenterHorizontally,
        verticalArrangement = Arrangement.spacedBy(4.dp)
    ) {
        Box(contentAlignment = Alignment.Center) {
            Icon(
                painter = painterResource(id = iconRes),
                contentDescription = null,
                modifier = Modifier
                    .size(64.dp)
                    .clip(RoundedCornerShape(4.dp))
                    .alpha(
                        when {
                            !enabled -> 0.3f       // Mờ 30% khi disabled
                            isProcessing -> 0.7f   // Mờ 70% khi processing
                            else -> 1f             // Full opacity khi normal
                        }
                    ),
                tint = Color.Unspecified  // Giữ nguyên màu gốc
            )

            if (isProcessing) {
                CircularProgressIndicator(
                    modifier = Modifier
                        .size(16.dp)
                        .align(Alignment.Center),
                    strokeWidth = 2.dp,
                    color = MaterialTheme.colorScheme.primary
                )
            }
        }

        Text(
            text = intValue.toString(),
            fontSize = 14.sp,
            color = MaterialTheme.colorScheme.onSurface.copy(
                alpha = when {
                    !enabled -> 0.3f
                    isProcessing -> 0.7f
                    else -> 1f
                }
            )
        )
    }
}