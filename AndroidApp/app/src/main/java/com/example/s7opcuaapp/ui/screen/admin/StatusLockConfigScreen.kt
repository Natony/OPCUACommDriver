package com.example.s7opcuaapp.ui.screen.admin

import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.ArrowBack
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.example.s7opcuaapp.util.StatusLockConfig.StatusLockRule
import com.example.s7opcuaapp.viewmodel.StatusLockConfigViewModel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun StatusLockConfigScreen(
    viewModel: StatusLockConfigViewModel = hiltViewModel(),
    onBack: () -> Unit
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()

    Scaffold(
        topBar = {
            TopAppBar(
                title = { Text("Button Lock Settings") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.Default.ArrowBack, "Back")
                    }
                },
                actions = {
                    TextButton(
                        onClick = { viewModel.showResetConfirmation() }
                    ) {
                        Text("Reset to Default", color = MaterialTheme.colorScheme.error)
                    }
                }
            )
        }
    ) { paddingValues ->
        LazyColumn(
            modifier = Modifier
                .fillMaxSize()
                .padding(paddingValues),
            contentPadding = PaddingValues(16.dp),
            verticalArrangement = Arrangement.spacedBy(12.dp)
        ) {
            // Master Override Section
            item {
                MasterOverrideCard(
                    isActive = uiState.overrideActive,
                    onToggle = { viewModel.toggleOverride() }
                )
            }

            // Section header
            item {
                Text(
                    text = "Lock Rules by Status",
                    style = MaterialTheme.typography.titleMedium,
                    fontWeight = FontWeight.Bold,
                    modifier = Modifier.padding(top = 8.dp, bottom = 4.dp)
                )
            }

            // Status rules
            val sortedRules = uiState.statusRules.toList().sortedBy { it.first }
            items(
                items = sortedRules,
                key = { it.first }
            ) { (status, rule) ->
                SimpleStatusRuleCard(
                    rule = rule,
                    enabled = !uiState.overrideActive,
                    onToggle = { viewModel.toggleRuleEnabled(status) },
                    onEditExemptions = { viewModel.showExemptButtonsDialog(status) }
                )
            }
        }
    }

    // Exemptions Dialog
    val editingStatus = uiState.editingStatus
    if (editingStatus != null) {
        ExemptButtonsDialog(
            statusValue = editingStatus,
            currentRule = uiState.statusRules[editingStatus],
            onToggleButton = { buttonIndex ->
                viewModel.toggleExemptButton(buttonIndex)
            },
            onDismiss = { viewModel.hideExemptButtonsDialog() }
        )
    }

    // Reset confirmation dialog
    if (uiState.showResetConfirmation) {
        AlertDialog(
            onDismissRequest = { viewModel.hideResetConfirmation() },
            title = { Text("Reset to Default?") },
            text = { Text("This will reset all lock settings to default values.") },
            confirmButton = {
                TextButton(
                    onClick = {
                        viewModel.resetToDefaults()
                        viewModel.hideResetConfirmation()
                    }
                ) {
                    Text("Reset", color = MaterialTheme.colorScheme.error)
                }
            },
            dismissButton = {
                TextButton(onClick = { viewModel.hideResetConfirmation() }) {
                    Text("Cancel")
                }
            }
        )
    }
}

@Composable
private fun ExemptButtonsDialog(
    statusValue: Int,
    currentRule: StatusLockRule?,
    onToggleButton: (Int) -> Unit,
    onDismiss: () -> Unit
) {
    if (currentRule == null) return

    AlertDialog(
        onDismissRequest = onDismiss,
        title = {
            Column {
                Text("Chọn nút không bị khóa")
                Text(
                    text = "Status ${currentRule.statusValue}: ${currentRule.description}",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        },
        text = {
            LazyColumn(
                modifier = Modifier.fillMaxWidth(),
                verticalArrangement = Arrangement.spacedBy(4.dp)
            ) {
                // Group buttons by type
                item {
                    Text(
                        text = "Nút điều khiển chung",
                        style = MaterialTheme.typography.labelMedium,
                        modifier = Modifier.padding(vertical = 4.dp)
                    )
                }

                // Common control buttons
                val commonButtons = listOf(
                    4 to "Power",
                    5 to "Buzzer",
                    10 to "Emergency Stop",
                    11 to "FIFO/LIFO",
                    13 to "Direction A/B",
                    14 to "Count Pallet",
                    999 to "Send All (Chạy)"
                )

                items(commonButtons) { (index, name) ->
                    ButtonExemptionItem(
                        buttonIndex = index,
                        buttonName = name,
                        isExempt = index in currentRule.exemptButtons,
                        onToggle = { onToggleButton(index) }
                    )
                }

                item {
                    Divider(modifier = Modifier.padding(vertical = 8.dp))
                    Text(
                        text = "Nút Manual",
                        style = MaterialTheme.typography.labelMedium,
                        modifier = Modifier.padding(vertical = 4.dp)
                    )
                }

                // Manual buttons
                items((0..3).toList()) { index ->
                    ButtonExemptionItem(
                        buttonIndex = index,
                        buttonName = "Manual ${index + 1}",
                        isExempt = index in currentRule.exemptButtons,
                        onToggle = { onToggleButton(index) }
                    )
                }

                item {
                    Divider(modifier = Modifier.padding(vertical = 8.dp))
                    Text(
                        text = "Nút Auto",
                        style = MaterialTheme.typography.labelMedium,
                        modifier = Modifier.padding(vertical = 4.dp)
                    )
                }

                // Auto buttons
                items((6..9).toList()) { index ->
                    ButtonExemptionItem(
                        buttonIndex = index,
                        buttonName = "Auto ${index - 5}",
                        isExempt = index in currentRule.exemptButtons,
                        onToggle = { onToggleButton(index) }
                    )
                }

                // Int buttons
                items(listOf(203 to "Pallets Out", 204 to "Pallets In")) { (index, name) ->
                    ButtonExemptionItem(
                        buttonIndex = index,
                        buttonName = name,
                        isExempt = index in currentRule.exemptButtons,
                        onToggle = { onToggleButton(index) }
                    )
                }
            }
        },
        confirmButton = {
            TextButton(onClick = onDismiss) {
                Text("Đóng")
            }
        }
    )
}

@Composable
private fun ButtonExemptionItem(
    buttonIndex: Int,
    buttonName: String,
    isExempt: Boolean,
    onToggle: () -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = if (isExempt)
                MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.5f)
            else MaterialTheme.colorScheme.surface
        )
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(horizontal = 16.dp, vertical = 12.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Text(
                text = buttonName,
                style = MaterialTheme.typography.bodyMedium,
                color = if (isExempt)
                    MaterialTheme.colorScheme.primary
                else MaterialTheme.colorScheme.onSurface
            )

            Checkbox(
                checked = isExempt,
                onCheckedChange = { onToggle() }
            )
        }
    }
}

// Helper function
private fun getButtonNames(buttonIndices: Set<Int>): String {
    return buttonIndices.sorted().joinToString(", ") { index ->
        when (index) {
            4 -> "Power"
            5 -> "Buzzer"
            10 -> "Emergency Stop"
            11 -> "FIFO/LIFO"
            13 -> "Direction"
            14 -> "Count"
            999 -> "Send All"
            in 0..3 -> "Manual ${index + 1}"
            in 6..9 -> "Auto ${index - 5}"
            203 -> "Pallets Out"
            204 -> "Pallets In"
            else -> "Button $index"
        }
    }
}

@Composable
private fun MasterOverrideCard(
    isActive: Boolean,
    onToggle: () -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = if (isActive)
                MaterialTheme.colorScheme.errorContainer.copy(alpha = 0.3f)
            else MaterialTheme.colorScheme.primaryContainer
        )
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            Column(modifier = Modifier.weight(1f)) {
                Row(
                    verticalAlignment = Alignment.CenterVertically,
                    horizontalArrangement = Arrangement.spacedBy(8.dp)
                ) {
                    Icon(
                        imageVector = Icons.Default.Warning,
                        contentDescription = null,
                        tint = if (isActive)
                            MaterialTheme.colorScheme.error
                        else MaterialTheme.colorScheme.primary,
                        modifier = Modifier.size(24.dp)
                    )
                    Text(
                        text = "Master Override",
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.Bold
                    )
                }

                Spacer(modifier = Modifier.height(4.dp))

                Text(
                    text = if (isActive)
                        "All locks DISABLED - All buttons work regardless of status"
                    else
                        "Locks ACTIVE - Buttons locked based on PLC status",
                    style = MaterialTheme.typography.bodySmall,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }

            Switch(
                checked = isActive,
                onCheckedChange = { onToggle() },
                colors = SwitchDefaults.colors(
                    checkedThumbColor = if (isActive)
                        MaterialTheme.colorScheme.error
                    else MaterialTheme.colorScheme.primary,
                    checkedTrackColor = if (isActive)
                        MaterialTheme.colorScheme.errorContainer
                    else MaterialTheme.colorScheme.primaryContainer
                )
            )
        }
    }
}

@Composable
private fun SimpleStatusRuleCard(
    rule: StatusLockRule,
    enabled: Boolean,
    onToggle: () -> Unit,
    onEditExemptions: () -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        colors = CardDefaults.cardColors(
            containerColor = when {
                !enabled -> MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.5f)
                rule.isEnabled -> MaterialTheme.colorScheme.surface
                else -> MaterialTheme.colorScheme.surfaceVariant.copy(alpha = 0.7f)
            }
        )
    ) {
        Column(
            modifier = Modifier
                .fillMaxWidth()
                .padding(16.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Column(modifier = Modifier.weight(1f)) {
                    Text(
                        text = "Status ${rule.statusValue}: ${rule.description}",
                        style = MaterialTheme.typography.bodyMedium,
                        fontWeight = FontWeight.Medium
                    )

                    Spacer(modifier = Modifier.height(2.dp))

                    Text(
                        text = when {
                            !enabled -> "Override active - all locks disabled"
                            !rule.isEnabled -> "Disabled - no locks for this status"
                            rule.lockAllButtons -> "Locks all buttons when in this status"
                            else -> "No locks for this status"
                        },
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant.copy(alpha = 0.8f)
                    )
                }

                Row(
                    horizontalArrangement = Arrangement.spacedBy(8.dp),
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    // Edit exemptions button - only show when rule is enabled and locks all
                    if (rule.isEnabled && rule.lockAllButtons && enabled) {
                        IconButton(
                            onClick = onEditExemptions,
                            modifier = Modifier.size(40.dp)
                        ) {
                            Icon(
                                imageVector = Icons.Default.Edit,
                                contentDescription = "Edit exemptions",
                                tint = MaterialTheme.colorScheme.primary,
                                modifier = Modifier.size(20.dp)
                            )
                        }
                    }

                    Switch(
                        checked = rule.isEnabled,
                        onCheckedChange = { onToggle() },
                        enabled = enabled
                    )
                }
            }

            // Show exemptions if any
            if (rule.exemptButtons.isNotEmpty() && rule.lockAllButtons && rule.isEnabled) {
                Spacer(modifier = Modifier.height(8.dp))

                Card(
                    colors = CardDefaults.cardColors(
                        containerColor = MaterialTheme.colorScheme.primaryContainer.copy(alpha = 0.3f)
                    ),
                    modifier = Modifier.fillMaxWidth()
                ) {
                    Row(
                        modifier = Modifier
                            .fillMaxWidth()
                            .padding(12.dp),
                        verticalAlignment = Alignment.CenterVertically
                    ) {
                        Text(
                            text = "Ngoại lệ:",
                            style = MaterialTheme.typography.bodySmall,
                            fontWeight = FontWeight.Medium,
                            color = MaterialTheme.colorScheme.primary
                        )
                        Spacer(modifier = Modifier.width(8.dp))
                        Text(
                            text = getButtonNames(rule.exemptButtons),
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }
            }
        }
    }
}