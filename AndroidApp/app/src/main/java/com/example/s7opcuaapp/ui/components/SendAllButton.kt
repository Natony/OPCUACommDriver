package com.example.s7opcuaapp.ui.components

import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.alpha
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp

/**
 * Send All button component với support cho các states:
 * - Auto/Manual mode
 * - Writing/Processing
 * - Locked by status
 */
@Composable
fun SendAllButton(
    isAutoMode: Boolean,
    isWriting: Boolean,
    isLocked: Boolean,
    onClick: () -> Unit,
    modifier: Modifier = Modifier
) {
    // Tính toán enabled state
    val isEnabled = !isAutoMode && !isWriting && !isLocked

    // Tính toán alpha cho visual feedback
    val alpha = when {
        isLocked -> 0.3f      // Mờ nhất khi bị khóa
        isAutoMode -> 0.5f    // Mờ vừa khi Auto mode
        isWriting -> 0.7f     // Mờ nhẹ khi đang xử lý
        else -> 1f            // Full opacity khi normal
    }

    // Tính toán màu nút
    val buttonColor = when {
        isLocked -> MaterialTheme.colorScheme.surfaceVariant
        isAutoMode -> MaterialTheme.colorScheme.primary.copy(alpha = 0.6f)
        else -> MaterialTheme.colorScheme.primary
    }

    // Tính toán text hiển thị
    val buttonText = when {
        isLocked -> "KHÓA"
        isWriting -> "Đang xử lý..."
        else -> "CHẠY"
    }

    // Tính toán text color
    val textColor = when {
        !isEnabled -> MaterialTheme.colorScheme.onSurfaceVariant
        else -> MaterialTheme.colorScheme.onPrimary
    }

    Button(
        onClick = onClick,
        enabled = isEnabled,
        modifier = modifier
            .fillMaxWidth()
            .alpha(alpha),
        colors = ButtonDefaults.buttonColors(
            containerColor = buttonColor,
            disabledContainerColor = MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.6f)
        )
    ) {
        Row(
            modifier = Modifier.fillMaxWidth(),
            horizontalArrangement = Arrangement.Center,
            verticalAlignment = Alignment.CenterVertically
        ) {
            // Loading indicator
            if (isWriting) {
                CircularProgressIndicator(
                    modifier = Modifier.size(12.dp),
                    color = textColor,
                    strokeWidth = 2.dp
                )
                Spacer(modifier = Modifier.width(4.dp))
            }

            // Button text
            Text(
                text = buttonText,
                textAlign = TextAlign.Center,
                fontSize = 10.sp,
                fontWeight = FontWeight.Bold,
                color = textColor
            )
        }
    }
}

/**
 * Preview-friendly version với default styling
 */
@Composable
fun SendAllButtonPreview() {
    Column(
        modifier = Modifier.padding(16.dp),
        verticalArrangement = Arrangement.spacedBy(8.dp)
    ) {
        // Normal state
        SendAllButton(
            isAutoMode = false,
            isWriting = false,
            isLocked = false,
            onClick = {}
        )

        // Auto mode
        SendAllButton(
            isAutoMode = true,
            isWriting = false,
            isLocked = false,
            onClick = {}
        )

        // Writing
        SendAllButton(
            isAutoMode = false,
            isWriting = true,
            isLocked = false,
            onClick = {}
        )

        // Locked
        SendAllButton(
            isAutoMode = false,
            isWriting = false,
            isLocked = true,
            onClick = {}
        )
    }
}