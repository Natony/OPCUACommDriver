package com.example.s7opcuaapp.ui.components

import androidx.compose.foundation.gestures.detectTapGestures
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.size
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.MaterialTheme
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.input.pointer.pointerInput
import androidx.compose.ui.res.painterResource
import androidx.compose.ui.unit.dp
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch
import androidx.compose.ui.draw.alpha

/**
 * Simple Press/Release button with guaranteed release on up
 */
@Composable
fun PressReleaseBoolControlItem(
    value: Boolean,
    iconOn: Int,
    iconOff: Int,
    onPress: () -> Unit,
    onRelease: () -> Unit,
    enabled: Boolean = true,
    modifier: Modifier = Modifier
) {
    var isPressed by remember { mutableStateOf(false) }

    DisposableEffect(enabled) {
        onDispose {
            if (isPressed) {
                onRelease()
                isPressed = false
            }
        }
    }

    Box(
        modifier = modifier
            .size(108.dp)
            .alpha(if (enabled) 1f else 0.3f)  // Mờ cả box khi disabled
            .then(
                if (enabled) {
                    Modifier.pointerInput(Unit) {
                        detectTapGestures(
                            onPress = {
                                isPressed = true
                                onPress()

                                val released = tryAwaitRelease()

                                isPressed = false
                                onRelease()
                            }
                        )
                    }
                } else {
                    Modifier
                }
            ),
        contentAlignment = Alignment.Center
    ) {
        Icon(
            painter = painterResource(id = if (value || isPressed) iconOn else iconOff),
            contentDescription = null,
            modifier = Modifier.size(108.dp),
            tint = when {
                isPressed -> Color(0xFFFFEB3B).copy(alpha = 0.8f)  // Vàng nhạt khi pressed
                else -> Color.Unspecified  // Giữ màu gốc
            }
        )
    }
}