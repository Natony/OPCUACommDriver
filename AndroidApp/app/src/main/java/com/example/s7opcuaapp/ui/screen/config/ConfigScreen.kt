package com.example.s7opcuaapp.ui.screen.config

import androidx.compose.foundation.background
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.*
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.example.s7opcuaapp.data.model.DeviceEntity
import com.example.s7opcuaapp.viewmodel.ConfigUiState
import com.example.s7opcuaapp.viewmodel.ControlViewModel

@Composable
fun ConfigScreen(
    uiState: ConfigUiState,
    connectionState: ControlViewModel.ConnectionState? = null,
    onNewDeviceNameChanged: (String) -> Unit,
    onNewDeviceIpChanged: (String) -> Unit,
    onNewDevicePortChanged: (String) -> Unit,
    onNewDeviceUsernameChanged: (String) -> Unit,
    onNewDevicePasswordChanged: (String) -> Unit,
    onAddDevice: () -> Unit,
    onRemoveDevice: (DeviceEntity) -> Unit,
    onSelectDevice: (DeviceEntity) -> Unit,
    onEditDevice: (DeviceEntity) -> Unit = {},
    onCancelEdit: () -> Unit = {}
) {
    LazyColumn(
        modifier = Modifier
            .fillMaxSize()
            .padding(12.dp),
        verticalArrangement = Arrangement.spacedBy(8.dp)
    ) {

        // Connection Status Card (if provided)
        connectionState?.let { state ->
            item {
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    colors = CardDefaults.cardColors(
                        containerColor = when (state) {
                            is ControlViewModel.ConnectionState.Connected -> Color(0xFFE8F5E9)
                            is ControlViewModel.ConnectionState.Connecting -> Color(0xFFFFF9C4)
                            is ControlViewModel.ConnectionState.Failed -> Color(0xFFFFEBEE)
                            else -> MaterialTheme.colorScheme.surfaceVariant
                        }
                    )
                ) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(12.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Icon(
                            imageVector = when (state) {
                                is ControlViewModel.ConnectionState.Connected -> Icons.Default.CheckCircle
                                is ControlViewModel.ConnectionState.Failed -> Icons.Default.Error
                                else -> Icons.Default.Info
                            },
                            contentDescription = null,
                            tint = when (state) {
                                is ControlViewModel.ConnectionState.Connected -> Color(0xFF4CAF50)
                                is ControlViewModel.ConnectionState.Failed -> Color(0xFFF44336)
                                else -> MaterialTheme.colorScheme.onSurfaceVariant
                            }
                        )
                        Spacer(modifier = Modifier.width(8.dp))
                        Text(
                            text = when (state) {
                                is ControlViewModel.ConnectionState.Connected -> "Connected to PLC"
                                is ControlViewModel.ConnectionState.Connecting -> "Connecting..."
                                is ControlViewModel.ConnectionState.Failed -> "Connection failed: ${state.error}"
                                is ControlViewModel.ConnectionState.Timeout -> "Connection timeout"
                                else -> "Not connected"
                            },
                            style = MaterialTheme.typography.bodyMedium
                        )
                    }
                }
            }
        }
        // Header
        item {
            Text(
                text = "Device Configuration",
                style = MaterialTheme.typography.headlineSmall,
                fontWeight = FontWeight.Bold,
                modifier = Modifier.padding(bottom = 4.dp)
            )
        }

        // Add/Edit Device Form
        item {
            Card(
                modifier = Modifier.fillMaxWidth(),
                elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
            ) {
                Column(
                    modifier = Modifier.padding(12.dp)
                ) {
                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(
                            text = if (uiState.isEditMode) "Edit Device" else "Add New Device",
                            style = MaterialTheme.typography.titleMedium,
                            fontWeight = FontWeight.SemiBold
                        )

                        if (uiState.isEditMode) {
                            IconButton(onClick = onCancelEdit) {
                                Icon(
                                    imageVector = Icons.Default.Close,
                                    contentDescription = "Cancel",
                                    tint = MaterialTheme.colorScheme.error
                                )
                            }
                        }
                    }

                    Spacer(modifier = Modifier.height(8.dp))

                    // Compact form fields
                    OutlinedTextField(
                        value = uiState.newDeviceName,
                        onValueChange = onNewDeviceNameChanged,
                        label = { Text("Device Name", fontSize = 12.sp) },
                        modifier = Modifier.fillMaxWidth(),
                        singleLine = true
                    )
                    Spacer(modifier = Modifier.height(6.dp))

                    Row(modifier = Modifier.fillMaxWidth()) {
                        OutlinedTextField(
                            value = uiState.newDeviceIp,
                            onValueChange = onNewDeviceIpChanged,
                            label = { Text("IP Address", fontSize = 12.sp) },
                            modifier = Modifier.weight(2f),
                            singleLine = true
                        )
                        Spacer(modifier = Modifier.width(8.dp))
                        OutlinedTextField(
                            value = uiState.newDevicePort,
                            onValueChange = onNewDevicePortChanged,
                            label = { Text("Port", fontSize = 12.sp) },
                            modifier = Modifier.weight(1f),
                            singleLine = true
                        )
                    }
                    Spacer(modifier = Modifier.height(6.dp))

                    Row(modifier = Modifier.fillMaxWidth()) {
                        OutlinedTextField(
                            value = uiState.newDeviceUsername,
                            onValueChange = onNewDeviceUsernameChanged,
                            label = { Text("Username (Optional)", fontSize = 12.sp) },
                            modifier = Modifier.weight(1f),
                            singleLine = true
                        )
                        Spacer(modifier = Modifier.width(8.dp))
                        OutlinedTextField(
                            value = uiState.newDevicePassword,
                            onValueChange = onNewDevicePasswordChanged,
                            label = { Text("Password (Optional)", fontSize = 12.sp) },
                            modifier = Modifier.weight(1f),
                            singleLine = true
                        )
                    }

                    Spacer(modifier = Modifier.height(8.dp))

                    Row(
                        modifier = Modifier.fillMaxWidth(),
                        horizontalArrangement = Arrangement.End
                    ) {
                        if (uiState.isEditMode) {
                            TextButton(onClick = onCancelEdit) {
                                Text("Cancel")
                            }
                            Spacer(modifier = Modifier.width(8.dp))
                        }

                        Button(
                            onClick = onAddDevice
                        ) {
                            Icon(
                                imageVector = if (uiState.isEditMode) Icons.Default.Check else Icons.Default.Add,
                                contentDescription = null,
                                modifier = Modifier.size(18.dp)
                            )
                            Spacer(modifier = Modifier.width(4.dp))
                            Text(if (uiState.isEditMode) "Update" else "Add Device")
                        }
                    }

                    uiState.errorMessage?.let { msg ->
                        Spacer(modifier = Modifier.height(8.dp))
                        Text(
                            text = msg,
                            color = MaterialTheme.colorScheme.error,
                            style = MaterialTheme.typography.bodySmall
                        )
                    }
                }
            }
        }

        // Device List Header
        item {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = "Saved Devices (${uiState.deviceList.size})",
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.SemiBold
                )

                if (uiState.currentDevice != null) {
                    Surface(
                        shape = MaterialTheme.shapes.small,
                        color = MaterialTheme.colorScheme.primaryContainer
                    ) {
                        Text(
                            text = "Current: ${uiState.currentDevice.name}",
                            modifier = Modifier.padding(horizontal = 12.dp, vertical = 4.dp),
                            style = MaterialTheme.typography.labelMedium,
                            color = MaterialTheme.colorScheme.onPrimaryContainer
                        )
                    }
                }
            }
        }

        // Device List Content
        if (uiState.deviceList.isEmpty()) {
            item {
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surfaceVariant)
                ) {
                    Text(
                        text = "No devices configured yet",
                        modifier = Modifier.padding(16.dp),
                        style = MaterialTheme.typography.bodyMedium,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }
        } else {
            items(uiState.deviceList) { device ->
                DeviceListItem(
                    device = device,
                    isSelected = (device.id == uiState.currentDevice?.id),
                    onRemove = { onRemoveDevice(device) },
                    onSelect = { onSelectDevice(device) },
                    onEdit = { onEditDevice(device) }
                )
            }
        }
    }
}

// ========== PREVIEW SECTION ==========

@Preview(showBackground = true, widthDp = 1920, heightDp = 1200)
@Composable
fun ConfigScreenWithDevicesPreview() {
    val sampleDevices = listOf(
        DeviceEntity(
            id = "1",
            name = "PLC-01",
            ipAddress = "192.168.1.100",
            port = 4840,
            opcUsername = "admin",
            opcPassword = "password",
            useOpcUa = true
        ),
        DeviceEntity(
            id = "2",
            name = "PLC-02",
            ipAddress = "192.168.1.101",
            port = 4840,
            opcUsername = "",
            opcPassword = "",
            useOpcUa = true
        )
    )

    val sampleState = ConfigUiState(
        newDeviceName = "",
        newDeviceIp = "",
        newDevicePort = "4840",
        newDeviceUsername = "",
        newDevicePassword = "",
        deviceList = sampleDevices,
        currentDevice = sampleDevices.first(),
        errorMessage = null
    )

    ConfigScreen(
        uiState = sampleState,
        connectionState = ControlViewModel.ConnectionState.Connected,
        onNewDeviceNameChanged = {},
        onNewDeviceIpChanged = {},
        onNewDevicePortChanged = {},
        onNewDeviceUsernameChanged = {},
        onNewDevicePasswordChanged = {},
        onAddDevice = {},
        onRemoveDevice = {},
        onSelectDevice = {}
    )
}