package com.example.s7opcuaapp.ui.components

import androidx.compose.foundation.layout.Box
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier

/**
 * A wrapper component that was meant to prevent multi-touch gestures
 * For now, just acts as a passthrough Box
 */
@Composable
fun SingleTouchHandler(
    modifier: Modifier = Modifier,
    content: @Composable () -> Unit
) {
    Box(modifier = modifier) {
        content()
    }
}