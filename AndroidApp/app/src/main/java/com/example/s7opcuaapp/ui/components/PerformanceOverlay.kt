package com.example.s7opcuaapp.ui.components

import androidx.compose.animation.AnimatedVisibility
import androidx.compose.animation.fadeIn
import androidx.compose.animation.fadeOut
import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.gestures.detectDragGestures
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Speed
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.draw.clip
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.platform.LocalDensity
import androidx.compose.ui.text.font.FontFamily
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.IntOffset
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.example.s7opcuaapp.viewmodel.PerformanceViewModel
import kotlin.math.roundToInt

@Composable
fun PerformanceOverlay(
    modifier: Modifier = Modifier,
    viewModel: PerformanceViewModel = hiltViewModel()
) {
    var expanded by remember { mutableStateOf(false) }
    var offsetX by remember { mutableStateOf(0f) }
    var offsetY by remember { mutableStateOf(0f) }
    var visible by remember { mutableStateOf(true) }

    val performanceData by viewModel.performanceData.collectAsStateWithLifecycle()
    val density = LocalDensity.current

    if (!visible) {
        // Mini floating button to restore
        Box(
            modifier = Modifier
                .offset { IntOffset(offsetX.roundToInt(), offsetY.roundToInt()) }
                .size(40.dp)
                .clip(RoundedCornerShape(20.dp))
                .background(Color.Black.copy(alpha = 0.7f))
                .clickable { visible = true }
                .pointerInput(Unit) {
                    detectDragGestures { _, dragAmount ->
                        offsetX += dragAmount.x
                        offsetY += dragAmount.y
                    }
                },
            contentAlignment = Alignment.Center
        ) {
            Icon(
                imageVector = Icons.Default.Speed,
                contentDescription = null,
                tint = Color.White,
                modifier = Modifier.size(20.dp)
            )
        }
        return
    }

    Surface(
        modifier = modifier
            .offset { IntOffset(offsetX.roundToInt(), offsetY.roundToInt()) }
            .widthIn(min = 120.dp, max = 200.dp)
            .clip(RoundedCornerShape(8.dp))
            .pointerInput(Unit) {
                detectDragGestures { _, dragAmount ->
                    offsetX += dragAmount.x
                    offsetY += dragAmount.y
                }
            },
        color = Color.Black.copy(alpha = 0.85f),
        contentColor = Color.White
    ) {
        Column(
            modifier = Modifier.padding(8.dp)
        ) {
            // Compact header
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    modifier = Modifier.weight(1f)
                ) {
                    Icon(
                        imageVector = Icons.Default.Speed,
                        contentDescription = null,
                        modifier = Modifier.size(14.dp),
                        tint = getPerformanceColor(performanceData.plcUpdateRate)
                    )
                    Spacer(modifier = Modifier.width(4.dp))
                    Text(
                        text = "${performanceData.plcUpdateRate.format(1)}/s",
                        fontSize = 11.sp,
                        fontWeight = FontWeight.Bold,
                        color = getPerformanceColor(performanceData.plcUpdateRate)
                    )
                }

                Row {
                    // Expand/Collapse button
                    IconButton(
                        onClick = { expanded = !expanded },
                        modifier = Modifier.size(20.dp)
                    ) {
                        Text(
                            text = if (expanded) "−" else "+",
                            fontSize = 12.sp,
                            fontWeight = FontWeight.Bold
                        )
                    }

                    // Hide button
                    IconButton(
                        onClick = { visible = false },
                        modifier = Modifier.size(20.dp)
                    ) {
                        Icon(
                            imageVector = Icons.Default.Close,
                            contentDescription = null,
                            modifier = Modifier.size(12.dp)
                        )
                    }
                }
            }

            // Memory usage (always visible)
            Text(
                text = "Mem: ${performanceData.memoryUsageMB.format(0)}MB",
                fontSize = 10.sp,
                color = getMemoryColor(performanceData.memoryUsageMB),
                fontFamily = FontFamily.Monospace
            )

            // Expanded details
            AnimatedVisibility(
                visible = expanded,
                enter = fadeIn(),
                exit = fadeOut()
            ) {
                Column(
                    modifier = Modifier.padding(top = 4.dp),
                    verticalArrangement = Arrangement.spacedBy(1.dp)
                ) {
                    Divider(color = Color.White.copy(alpha = 0.3f))

                    CompactMetric("UI", "${performanceData.uiRecompositionRate.format(1)}/s")
                    CompactMetric("Write", "${performanceData.writeCommandRate.format(1)}/s")
                    CompactMetric("Latency", "${performanceData.avgNetworkLatency.format(0)}ms")
                    CompactMetric("Subs", performanceData.activeSubscriptions.toString())

                    Divider(
                        color = Color.White.copy(alpha = 0.3f),
                        modifier = Modifier.padding(vertical = 2.dp)
                    )

                    Text(
                        text = "Reset",
                        fontSize = 9.sp,
                        color = Color.Cyan,
                        modifier = Modifier
                            .clickable { viewModel.resetStats() }
                            .padding(vertical = 2.dp)
                    )
                }
            }
        }
    }
}

@Composable
private fun CompactMetric(
    label: String,
    value: String
) {
    Row(
        modifier = Modifier.fillMaxWidth(),
        horizontalArrangement = Arrangement.SpaceBetween
    ) {
        Text(
            text = label,
            fontSize = 9.sp,
            color = Color.White.copy(alpha = 0.7f)
        )
        Text(
            text = value,
            fontSize = 9.sp,
            fontFamily = FontFamily.Monospace
        )
    }
}

private fun getPerformanceColor(updateRate: Double): Color {
    return when {
        updateRate < 2 -> Color.Green
        updateRate < 10 -> Color.Yellow
        else -> Color.Red
    }
}

private fun getMemoryColor(memoryMB: Double): Color {
    return when {
        memoryMB < 150 -> Color.Green
        memoryMB < 250 -> Color.Yellow
        else -> Color.Red
    }
}

private fun Double.format(decimals: Int): String = "%.${decimals}f".format(this)