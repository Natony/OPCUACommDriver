package com.example.s7opcuaapp.ui.screen.history

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.AccessTime
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.ErrorOutline
import androidx.compose.material.icons.filled.GetApp
import androidx.compose.material.icons.filled.Input
import androidx.compose.material.icons.filled.People
import androidx.compose.material.icons.filled.Person
import androidx.compose.material.icons.filled.Phone
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.example.s7opcuaapp.data.model.User
import com.example.s7opcuaapp.viewmodel.DateRange
import com.example.s7opcuaapp.viewmodel.LoginHistoryItem
import com.example.s7opcuaapp.viewmodel.LoginHistoryViewModel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun LoginHistoryScreen(
    viewModel: LoginHistoryViewModel
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()

    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp)
    ) {
        // Header with stats
        LoginHistoryHeader(
            totalLogins = uiState.totalLogins,
            successfulLogins = uiState.successfulLogins,
            failedLogins = uiState.failedLogins,
            activeUsers = uiState.activeUsers
        )

        Spacer(modifier = Modifier.height(16.dp))

        // Filters
        LoginHistoryFilters(
            selectedDateRange = uiState.selectedDateRange,
            selectedUser = uiState.selectedUser,
            showOnlyFailures = uiState.showOnlyFailures,
            userList = uiState.userList,
            onDateRangeSelected = viewModel::selectDateRange,
            onUserSelected = viewModel::selectUser,
            onToggleShowFailures = viewModel::toggleShowOnlyFailures,
            onExport = viewModel::exportToCSV
        )

        Spacer(modifier = Modifier.height(16.dp))

        // History list
        if (uiState.isLoading) {
            Box(
                modifier = Modifier.fillMaxSize(),
                contentAlignment = Alignment.Center
            ) {
                CircularProgressIndicator()
            }
        } else if (uiState.loginHistoryList.isEmpty()) {
            Box(
                modifier = Modifier.fillMaxSize(),
                contentAlignment = Alignment.Center
            ) {
                Text(
                    text = "Không có lịch sử đăng nhập",
                    style = MaterialTheme.typography.bodyLarge,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        } else {
            LazyColumn(
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                items(
                    items = uiState.loginHistoryList,
                    key = { it.history.id }
                ) { item ->
                    LoginHistoryItemCard(item)
                }
            }
        }

        // Error message
        uiState.errorMessage?.let { error ->
            Snackbar(
                modifier = Modifier.padding(16.dp),
                action = {
                    TextButton(onClick = { /* Dismiss */ }) {
                        Text("Đóng")
                    }
                }
            ) {
                Text(error)
            }
        }
    }
}

@Composable
private fun LoginHistoryHeader(
    totalLogins: Int,
    successfulLogins: Int,
    failedLogins: Int,
    activeUsers: Int
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
    ) {
        Column(
            modifier = Modifier.padding(16.dp)
        ) {
            Text(
                text = "Lịch sử đăng nhập",
                style = MaterialTheme.typography.headlineSmall,
                fontWeight = FontWeight.Bold
            )

            Spacer(modifier = Modifier.height(12.dp))

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceEvenly
            ) {
                StatItem(
                    label = "Tổng đăng nhập",
                    value = totalLogins.toString(),
                    icon = Icons.Default.Input,
                    color = MaterialTheme.colorScheme.primary
                )
                StatItem(
                    label = "Thành công",
                    value = successfulLogins.toString(),
                    icon = Icons.Default.CheckCircle,
                    color = Color(0xFF4CAF50)
                )
                StatItem(
                    label = "Thất bại",
                    value = failedLogins.toString(),
                    icon = Icons.Default.ErrorOutline,
                    color = Color(0xFFF44336)
                )
                StatItem(
                    label = "Người dùng",
                    value = activeUsers.toString(),
                    icon = Icons.Default.People,
                    color = MaterialTheme.colorScheme.secondary
                )
            }
        }
    }
}

@Composable
private fun StatItem(
    label: String,
    value: String,
    icon: androidx.compose.ui.graphics.vector.ImageVector,
    color: Color
) {
    Column(
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Icon(
            imageVector = icon,
            contentDescription = null,
            tint = color,
            modifier = Modifier.size(24.dp)
        )
        Text(
            text = value,
            style = MaterialTheme.typography.titleLarge,
            fontWeight = FontWeight.Bold,
            color = color
        )
        Text(
            text = label,
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun LoginHistoryFilters(
    selectedDateRange: DateRange,
    selectedUser: User?,
    showOnlyFailures: Boolean,
    userList: List<User>,
    onDateRangeSelected: (DateRange) -> Unit,
    onUserSelected: (User?) -> Unit,
    onToggleShowFailures: () -> Unit,
    onExport: () -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Column(
            modifier = Modifier.padding(12.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                // Date range filter
                var expandedDateRange by remember { mutableStateOf(false) }
                ExposedDropdownMenuBox(
                    expanded = expandedDateRange,
                    onExpandedChange = { expandedDateRange = it },
                    modifier = Modifier.weight(1f)
                ) {
                    OutlinedTextField(
                        value = getDateRangeText(selectedDateRange),
                        onValueChange = {},
                        readOnly = true,
                        label = { Text("Thời gian") },
                        trailingIcon = {
                            ExposedDropdownMenuDefaults.TrailingIcon(expanded = expandedDateRange)
                        },
                        modifier = Modifier
                            .menuAnchor()
                            .fillMaxWidth()
                    )
                    ExposedDropdownMenu(
                        expanded = expandedDateRange,
                        onDismissRequest = { expandedDateRange = false }
                    ) {
                        DateRange.values().forEach { range ->
                            DropdownMenuItem(
                                text = { Text(getDateRangeText(range)) },
                                onClick = {
                                    onDateRangeSelected(range)
                                    expandedDateRange = false
                                }
                            )
                        }
                    }
                }

                Spacer(modifier = Modifier.width(8.dp))

                // User filter
                var expandedUser by remember { mutableStateOf(false) }
                ExposedDropdownMenuBox(
                    expanded = expandedUser,
                    onExpandedChange = { expandedUser = it },
                    modifier = Modifier.weight(1f)
                ) {
                    OutlinedTextField(
                        value = selectedUser?.username ?: "Tất cả",
                        onValueChange = {},
                        readOnly = true,
                        label = { Text("Người dùng") },
                        trailingIcon = {
                            ExposedDropdownMenuDefaults.TrailingIcon(expanded = expandedUser)
                        },
                        modifier = Modifier
                            .menuAnchor()
                            .fillMaxWidth()
                    )
                    ExposedDropdownMenu(
                        expanded = expandedUser,
                        onDismissRequest = { expandedUser = false }
                    ) {
                        DropdownMenuItem(
                            text = { Text("Tất cả") },
                            onClick = {
                                onUserSelected(null)
                                expandedUser = false
                            }
                        )
                        userList.forEach { user ->
                            DropdownMenuItem(
                                text = { Text(user.username) },
                                onClick = {
                                    onUserSelected(user)
                                    expandedUser = false
                                }
                            )
                        }
                    }
                }
            }

            Spacer(modifier = Modifier.height(8.dp))

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                // Show only failures checkbox
                Row(
                    verticalAlignment = Alignment.CenterVertically
                ) {
                    Checkbox(
                        checked = showOnlyFailures,
                        onCheckedChange = { onToggleShowFailures() }
                    )
                    Text(
                        text = "Chỉ hiện thất bại",
                        style = MaterialTheme.typography.bodyMedium
                    )
                }

                // Export button
                Button(
                    onClick = onExport,
                    contentPadding = PaddingValues(horizontal = 16.dp, vertical = 8.dp)
                ) {
                    Icon(
                        imageVector = Icons.Default.GetApp,
                        contentDescription = null,
                        modifier = Modifier.size(18.dp)
                    )
                    Spacer(modifier = Modifier.width(4.dp))
                    Text("Xuất CSV")
                }
            }
        }
    }
}

@Composable
private fun LoginHistoryItemCard(
    item: LoginHistoryItem
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        elevation = CardDefaults.cardElevation(defaultElevation = 1.dp)
    ) {
        Row(
            modifier = Modifier
                .fillMaxWidth()
                .padding(12.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically
        ) {
            // User info and time
            Column(modifier = Modifier.weight(1f)) {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(
                        imageVector = Icons.Default.Person,
                        contentDescription = null,
                        modifier = Modifier.size(20.dp),
                        tint = MaterialTheme.colorScheme.primary
                    )
                    Spacer(modifier = Modifier.width(4.dp))
                    Text(
                        text = item.history.username,
                        style = MaterialTheme.typography.titleMedium,
                        fontWeight = FontWeight.Medium
                    )
                }

                Spacer(modifier = Modifier.height(4.dp))

                Row(verticalAlignment = Alignment.CenterVertically) {
                    Icon(
                        imageVector = Icons.Default.AccessTime,
                        contentDescription = null,
                        modifier = Modifier.size(16.dp),
                        tint = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                    Spacer(modifier = Modifier.width(4.dp))
                    Text(
                        text = item.loginTimeText,
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }

                if (item.history.deviceInfo != null) {
                    Spacer(modifier = Modifier.height(2.dp))
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Icon(
                            imageVector = Icons.Default.Phone,
                            contentDescription = null,
                            modifier = Modifier.size(16.dp),
                            tint = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                        Spacer(modifier = Modifier.width(4.dp))
                        Text(
                            text = item.history.deviceInfo,
                            style = MaterialTheme.typography.bodySmall,
                            color = MaterialTheme.colorScheme.onSurfaceVariant
                        )
                    }
                }
            }

            // Duration and status - FIX: Sửa AssistChip
            Column(
                horizontalAlignment = Alignment.End
            ) {
                // FIX: Sử dụng SuggestionChip thay vì AssistChip
                SuggestionChip(
                    onClick = { },
                    label = {
                        Text(
                            text = item.statusText,
                            color = Color(item.statusColor)
                        )
                    },
                    colors = SuggestionChipDefaults.suggestionChipColors(
                        containerColor = Color(item.statusColor).copy(alpha = 0.2f)
                    ),
                    border = BorderStroke(
                        width = 1.dp,
                        color = Color(item.statusColor)
                    )
                )

                if (item.history.logoutTime != null) {
                    Spacer(modifier = Modifier.height(4.dp))
                    Text(
                        text = "Thời gian: ${item.durationText}",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant
                    )
                }
            }
        }
    }
}

private fun getDateRangeText(dateRange: DateRange): String {
    return when (dateRange) {
        DateRange.TODAY -> "Hôm nay"
        DateRange.YESTERDAY -> "Hôm qua"
        DateRange.LAST_WEEK -> "7 ngày qua"
        DateRange.LAST_MONTH -> "30 ngày qua"
        DateRange.CUSTOM -> "Tùy chỉnh"
    }
}