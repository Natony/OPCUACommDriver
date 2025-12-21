package com.example.s7opcuaapp.ui.components

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.size
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.unit.dp
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.ui.Alignment
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.graphics.ColorFilter

/**
 * Composable chỉ hiển thị icon tương ứng boolean state,
 * không có nền, label hay indicator.
 */
@Composable
fun BoolControlItem(
    value: Boolean,
    iconOn: Int,
    iconOff: Int,
    onClick: () -> Unit,
    enabled: Boolean = true,
    isProcessing: Boolean = false,
    modifier: Modifier = Modifier
) {
    Box(
        modifier = modifier
            .size(108.dp)
            .clickable(enabled = enabled && !isProcessing) { onClick() },
        contentAlignment = Alignment.Center
    ) {
        Icon(
            painter = painterResource(id = if (value) iconOn else iconOff),
            contentDescription = if (value) "On" else "Off",
            modifier = Modifier
                .size(108.dp)
                .alpha(
                    when {
                        !enabled -> 0.3f       // Mờ 30% khi disabled
                        isProcessing -> 0.7f   // Mờ 70% khi processing
                        else -> 1f             // Full opacity khi normal
                    }
                ),
            tint = Color.Unspecified  // KHÔNG tint màu, giữ nguyên màu gốc
        )

        // Show loading indicator if processing
        if (isProcessing) {
            CircularProgressIndicator(
                modifier = Modifier
                    .size(28.dp)
                    .align(Alignment.Center),
                strokeWidth = 3.dp,
                color = MaterialTheme.colorScheme.primary
            )
        }
    }
}