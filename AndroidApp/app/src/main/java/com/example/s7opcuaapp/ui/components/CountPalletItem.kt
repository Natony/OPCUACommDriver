package com.example.s7opcuaapp.ui.components

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.compose.ui.draw.alpha
import com.example.s7opcuaapp.R

/**
 * Count Pallet component hiển thị số lượng và nút đếm
 * @param count Số lượng pallet (từ ints[2])
 * @param isPressed Trạng thái nút (từ bools[13])
 * @param isManualMode True khi đang ở chế độ manual
 * @param onClick Callback khi nhấn nút (chỉ hoạt động ở manual mode)
 */

@Composable
fun CountPalletItem(
    count: Int,
    isPressed: Boolean,
    isManualMode: Boolean,
    onClick: () -> Unit,
    enabled: Boolean = true,
    isProcessing: Boolean = false,
    modifier: Modifier = Modifier
) {
    Column(
        modifier = modifier
            .wrapContentSize()
            .padding(4.dp),
        verticalArrangement = Arrangement.spacedBy(4.dp),
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        // Số lượng - luôn hiển thị
        Card(
            modifier = Modifier.fillMaxWidth(),
            colors = CardDefaults.cardColors(
                containerColor = when {
                    isProcessing -> MaterialTheme.colorScheme.primaryContainer
                    else -> MaterialTheme.colorScheme.surfaceVariant
                }
            ),
            elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
        ) {
            Box(
                modifier = Modifier
                    .fillMaxWidth()
                    .padding(horizontal = 6.dp, vertical = 2.dp),
                contentAlignment = Alignment.Center
            ) {
                Text(
                    text = count.toString(),
                    fontSize = 10.sp,
                    fontWeight = FontWeight.Bold,
                    color = MaterialTheme.colorScheme.primary.copy(
                        alpha = when {
                            !isManualMode -> 0.5f
                            isProcessing -> 0.7f
                            else -> 1f
                        }
                    )
                )
            }
        }

        // Nút đếm
        Box(
            modifier = Modifier
                .size(78.dp)
                .clip(RoundedCornerShape(8.dp))
                .background(
                    when {
                        isProcessing -> Color.Yellow.copy(alpha = 0.1f)
                        isPressed -> MaterialTheme.colorScheme.primary.copy(alpha = 0.1f)
                        else -> Color.Transparent
                    }
                )
                .alpha(
                    when {
                        !isManualMode -> 0.3f  // Mờ khi auto mode
                        !enabled -> 0.3f       // Mờ khi disabled
                        else -> 1f
                    }
                )
                .then(
                    if (isManualMode && !isProcessing && enabled) {
                        Modifier.clickable { onClick() }
                    } else {
                        Modifier
                    }
                ),
            contentAlignment = Alignment.Center
        ) {
            Icon(
                painter = painterResource(
                    id = if (isPressed) R.drawable.ic_count_pallet_on
                    else R.drawable.ic_count_pallet_off
                ),
                contentDescription = "Count Pallet",
                modifier = Modifier.size(56.dp),
                tint = Color.Unspecified  // Giữ màu gốc của icon
            )

            if (isProcessing) {
                CircularProgressIndicator(
                    modifier = Modifier.size(20.dp),
                    strokeWidth = 2.dp,
                    color = MaterialTheme.colorScheme.primary
                )
            }
        }
    }
}