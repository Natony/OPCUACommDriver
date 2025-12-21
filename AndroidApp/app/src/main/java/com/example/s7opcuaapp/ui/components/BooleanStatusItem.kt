// File: BooleanStatusItem.kt
package com.example.s7opcuaapp.ui.components

import androidx.compose.foundation.background
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

/**
 * Hiển thị trạng thái Boolean (On/Off) với style giống BoolControlItem
 * ★ Read‑only, không gửi hay toggle gì
 */
@Composable
fun BooleanStatusItem(
    label: String,
    value: Boolean,
    iconOn: Int,
    iconOff: Int,
    modifier: Modifier = Modifier
) {

    Box(
        modifier = Modifier
            .size(156.dp)
            .clip(RoundedCornerShape(8.dp)),
        contentAlignment = Alignment.Center
    ) {
        Icon(
            painter = painterResource(id = if (value) iconOn else iconOff),
            contentDescription = null,
            modifier = Modifier.size(156.dp),
            tint = Color.Unspecified
        )
    }
}
