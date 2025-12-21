package com.example.s7opcuaapp.ui.components

import androidx.compose.foundation.layout.*
import androidx.compose.material3.*
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.unit.dp

@Composable
internal fun LoadingOverlay(
    message: String,
    loadingPercent: Int? = null,
    isIndeterminate: Boolean = true
) {
    FullScreenOverlay {
        Column(
            horizontalAlignment = Alignment.CenterHorizontally,
            verticalArrangement = Arrangement.spacedBy(16.dp)
        ) {
            if (isIndeterminate || loadingPercent == null) {
                CircularProgressIndicator(
                    modifier = Modifier.size(64.dp),
                    color = MaterialTheme.colorScheme.primary
                )
            } else {
                CircularProgressIndicator(
                    progress = loadingPercent / 100f,
                    modifier = Modifier.size(64.dp)
                )
            }

            Text(
                text = message,
                color = Color.White,
                style = MaterialTheme.typography.bodyLarge
            )

            loadingPercent?.let {
                Text(
                    text = "$it%",
                    color = Color.White,
                    style = MaterialTheme.typography.bodyMedium
                )
            }
        }
    }
}