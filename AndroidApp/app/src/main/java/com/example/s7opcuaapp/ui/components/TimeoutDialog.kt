package com.example.s7opcuaapp.ui.components

import androidx.compose.foundation.layout.*
import androidx.compose.material.icons.*
import androidx.compose.material.icons.filled.WifiOff
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import androidx.compose.material.icons.Icons
import androidx.compose.material3.Icon

@Composable
internal fun TimeoutDialog(
    timeoutCountdown: Int,
    onRetryConnection: () -> Unit,
    onNavigateToConfig: () -> Unit,
    onContinueOffline: () -> Unit
) {
    AlertDialog(
        onDismissRequest = { /* Can't dismiss */ },
        title = { Text("Connection Timeout") },
        text = {
            Column {
                Text("Unable to connect to PLC.")
                Spacer(modifier = Modifier.height(8.dp))
                Text(
                    "Returning to config in $timeoutCountdown seconds...",
                    style = MaterialTheme.typography.bodyMedium,
                    color = MaterialTheme.colorScheme.error
                )
                Spacer(modifier = Modifier.height(12.dp))

                Text(
                    "You can continue in offline mode to view the interface without PLC connection.",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )

            }
        },
        confirmButton = {
            OutlinedButton(
                onClick = onContinueOffline,
                colors = ButtonDefaults.outlinedButtonColors(
                    contentColor = MaterialTheme.colorScheme.secondary
                )
            ) {
                Icon(
                    imageVector = Icons.Default.WifiOff,
                    contentDescription = null,
                    modifier = Modifier.size(18.dp)
                )
                Spacer(modifier = Modifier.width(4.dp))
                Text("Offline Mode")
            }

            TextButton(onClick = onRetryConnection) {
                Text("Retry Connection")
            }
        },
        dismissButton = {
            TextButton(onClick = onNavigateToConfig) {
                Text("Go to Config")
            }
        }
    )
}