package com.example.s7opcuaapp.ui.screen.usermanager

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.*
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material.icons.filled.Edit
import androidx.compose.material.icons.filled.Lock
import androidx.compose.material.icons.filled.MoreVert
import androidx.compose.material3.*
import androidx.compose.runtime.*
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.example.s7opcuaapp.data.model.User
import com.example.s7opcuaapp.data.model.UserRole
import com.example.s7opcuaapp.ui.components.*
import com.example.s7opcuaapp.viewmodel.UserManagerViewModel

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun UserManagerScreen(
    viewModel: UserManagerViewModel
) {
    val uiState by viewModel.uiState.collectAsStateWithLifecycle()

    Column(
        modifier = Modifier
            .fillMaxSize()
            .padding(16.dp)
    ) {
        // Header with add button
        UserManagerHeader(
            totalUsers = uiState.totalUsers,
            activeUsers = uiState.activeUsers,
            adminCount = uiState.adminCount,
            operatorCount = uiState.operatorCount,
            viewerCount = uiState.viewerCount,
            onAddUser = viewModel::showAddUserDialog
        )

        Spacer(modifier = Modifier.height(16.dp))

        // User list
        if (uiState.isLoading) {
            Box(
                modifier = Modifier.fillMaxSize(),
                contentAlignment = Alignment.Center
            ) {
                CircularProgressIndicator()
            }
        } else if (uiState.userList.isEmpty()) {
            Box(
                modifier = Modifier.fillMaxSize(),
                contentAlignment = Alignment.Center
            ) {
                Text(
                    text = "Chưa có người dùng nào",
                    style = MaterialTheme.typography.bodyLarge,
                    color = MaterialTheme.colorScheme.onSurfaceVariant
                )
            }
        } else {
            LazyColumn(
                verticalArrangement = Arrangement.spacedBy(8.dp)
            ) {
                items(
                    items = uiState.userList,
                    key = { it.id }
                ) { user ->
                    UserItemCard(
                        user = user,
                        onEdit = { viewModel.showEditUserDialog(user) },
                        onChangePassword = { viewModel.showChangePasswordDialog(user) },
                        onToggleStatus = { viewModel.toggleUserStatus(user) },
                        onDelete = { viewModel.showDeleteConfirmation(user) }
                    )
                }
            }
        }
    }

    // Dialogs
    if (uiState.showAddEditDialog) {
        AddEditUserDialog(
            isEdit = uiState.editingUser != null,
            username = uiState.dialogUsername,
            password = uiState.dialogPassword,
            confirmPassword = uiState.dialogConfirmPassword,
            role = uiState.dialogRole,
            passwordStrength = uiState.dialogPasswordStrength,
            errorMessage = uiState.errorMessage,
            onUsernameChange = viewModel::updateDialogUsername,
            onPasswordChange = viewModel::updateDialogPassword,
            onConfirmPasswordChange = viewModel::updateDialogConfirmPassword,
            onRoleChange = viewModel::updateDialogRole,
            onSave = viewModel::saveUser,
            onDismiss = viewModel::hideAddEditDialog
        )
    }

    if (uiState.showChangePasswordDialog) {
        ChangePasswordDialog(
            username = uiState.changingPasswordForUser?.username ?: "",
            newPassword = uiState.newPassword,
            confirmPassword = uiState.confirmNewPassword,
            passwordStrength = uiState.newPasswordStrength,
            errorMessage = uiState.errorMessage,
            onNewPasswordChange = viewModel::updateNewPassword,
            onConfirmPasswordChange = viewModel::updateConfirmNewPassword,
            onSave = viewModel::changePassword,
            onDismiss = viewModel::hideChangePasswordDialog
        )
    }

    if (uiState.showDeleteConfirmation) {
        DeleteConfirmationDialog(
            username = uiState.userToDelete?.username ?: "",
            onConfirm = viewModel::confirmDeleteUser,
            onDismiss = viewModel::hideDeleteConfirmation
        )
    }

    // Messages
    uiState.successMessage?.let { message ->
        LaunchedEffect(message) {
            kotlinx.coroutines.delay(3000)
            viewModel.clearMessages()
        }

        Snackbar(
            modifier = Modifier.padding(16.dp),
            containerColor = Color(0xFF4CAF50)
        ) {
            Text(message)
        }
    }

    uiState.errorMessage?.let { error ->
        if (!uiState.showAddEditDialog && !uiState.showChangePasswordDialog) {
            Snackbar(
                modifier = Modifier.padding(16.dp),
                containerColor = MaterialTheme.colorScheme.error
            ) {
                Text(error)
            }
        }
    }
}

@Composable
private fun UserManagerHeader(
    totalUsers: Int,
    activeUsers: Int,
    adminCount: Int,
    operatorCount: Int,
    viewerCount: Int,
    onAddUser: () -> Unit
) {
    Card(
        modifier = Modifier.fillMaxWidth(),
        elevation = CardDefaults.cardElevation(defaultElevation = 2.dp)
    ) {
        Column(
            modifier = Modifier.padding(16.dp)
        ) {
            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceBetween,
                verticalAlignment = Alignment.CenterVertically
            ) {
                Text(
                    text = "Quản lý người dùng",
                    style = MaterialTheme.typography.headlineSmall,
                    fontWeight = FontWeight.Bold
                )

                FloatingActionButton(
                    onClick = onAddUser,
                    modifier = Modifier.size(48.dp)
                ) {
                    Icon(
                        imageVector = Icons.Default.Add,
                        contentDescription = "Thêm người dùng"
                    )
                }
            }

            Spacer(modifier = Modifier.height(12.dp))

            Row(
                modifier = Modifier.fillMaxWidth(),
                horizontalArrangement = Arrangement.SpaceEvenly
            ) {
                RoleStatItem(
                    role = "Admin",
                    count = adminCount,
                    color = Color(0xFFE91E63)
                )
                RoleStatItem(
                    role = "Operator",
                    count = operatorCount,
                    color = Color(0xFF2196F3)
                )
                RoleStatItem(
                    role = "Viewer",
                    count = viewerCount,
                    color = Color(0xFF4CAF50)
                )
                RoleStatItem(
                    role = "Hoạt động",
                    count = activeUsers,
                    color = MaterialTheme.colorScheme.primary
                )
            }
        }
    }
}

@Composable
private fun RoleStatItem(
    role: String,
    count: Int,
    color: Color
) {
    Column(
        horizontalAlignment = Alignment.CenterHorizontally
    ) {
        Text(
            text = count.toString(),
            style = MaterialTheme.typography.titleLarge,
            fontWeight = FontWeight.Bold,
            color = color
        )
        Text(
            text = role,
            style = MaterialTheme.typography.bodySmall,
            color = MaterialTheme.colorScheme.onSurfaceVariant
        )
    }
}

@Composable
private fun UserItemCard(
    user: User,
    onEdit: () -> Unit,
    onChangePassword: () -> Unit,
    onToggleStatus: () -> Unit,
    onDelete: () -> Unit
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
            // User info
            Row(
                modifier = Modifier.weight(1f),
                verticalAlignment = Alignment.CenterVertically
            ) {
                // Avatar
                Surface(
                    modifier = Modifier.size(48.dp),
                    shape = MaterialTheme.shapes.medium,
                    color = getRoleColor(user.role).copy(alpha = 0.2f)
                ) {
                    Box(
                        contentAlignment = Alignment.Center
                    ) {
                        Text(
                            text = user.username.first().uppercase(),
                            style = MaterialTheme.typography.titleMedium,
                            fontWeight = FontWeight.Bold,
                            color = getRoleColor(user.role)
                        )
                    }
                }

                Spacer(modifier = Modifier.width(12.dp))

                Column {
                    Row(verticalAlignment = Alignment.CenterVertically) {
                        Text(
                            text = user.username,
                            style = MaterialTheme.typography.titleMedium,
                            fontWeight = FontWeight.Medium
                        )
                        if (!user.isActive) {
                            Spacer(modifier = Modifier.width(8.dp))
                            Surface(
                                shape = MaterialTheme.shapes.small,
                                color = MaterialTheme.colorScheme.errorContainer
                            ) {
                                Text(
                                    text = "Vô hiệu",
                                    modifier = Modifier.padding(horizontal = 8.dp, vertical = 2.dp),
                                    style = MaterialTheme.typography.labelSmall,
                                    color = MaterialTheme.colorScheme.onErrorContainer
                                )
                            }
                        }
                    }

                    Spacer(modifier = Modifier.height(2.dp))

                    // FIX: Sử dụng SuggestionChip thay vì AssistChip
                    SuggestionChip(
                        onClick = { },
                        label = {
                            Text(
                                text = getRoleText(user.role),
                                color = getRoleColor(user.role)
                            )
                        },
                        colors = SuggestionChipDefaults.suggestionChipColors(
                            containerColor = getRoleColor(user.role).copy(alpha = 0.2f)
                        ),
                        border = BorderStroke(
                            width = 1.dp,
                            color = getRoleColor(user.role)
                        )
                    )
                }
            }

            // Actions
            Row {
                var expanded by remember { mutableStateOf(false) }

                IconButton(onClick = { expanded = true }) {
                    Icon(
                        imageVector = Icons.Default.MoreVert,
                        contentDescription = "More actions"
                    )
                }

                DropdownMenu(
                    expanded = expanded,
                    onDismissRequest = { expanded = false }
                ) {
                    DropdownMenuItem(
                        text = { Text("Chỉnh sửa") },
                        leadingIcon = {
                            Icon(
                                imageVector = Icons.Default.Edit,
                                contentDescription = null
                            )
                        },
                        onClick = {
                            expanded = false
                            onEdit()
                        }
                    )

                    DropdownMenuItem(
                        text = { Text("Đổi mật khẩu") },
                        leadingIcon = {
                            Icon(
                                imageVector = Icons.Default.Lock,
                                contentDescription = null
                            )
                        },
                        onClick = {
                            expanded = false
                            onChangePassword()
                        }
                    )

                    DropdownMenuItem(
                        text = {
                            Text(if (user.isActive) "Vô hiệu hóa" else "Kích hoạt")
                        },
                        leadingIcon = {
                            Icon(
                                imageVector = if (user.isActive) Icons.Default.Close else Icons.Default.CheckCircle,
                                contentDescription = null
                            )
                        },
                        onClick = {
                            expanded = false
                            onToggleStatus()
                        }
                    )

                    HorizontalDivider()

                    DropdownMenuItem(
                        text = {
                            Text(
                                "Xóa",
                                color = MaterialTheme.colorScheme.error
                            )
                        },
                        leadingIcon = {
                            Icon(
                                imageVector = Icons.Default.Delete,
                                contentDescription = null,
                                tint = MaterialTheme.colorScheme.error
                            )
                        },
                        onClick = {
                            expanded = false
                            onDelete()
                        }
                    )
                }
            }
        }
    }
}

private fun getRoleColor(role: UserRole): Color {
    return when (role) {
        UserRole.ADMIN -> Color(0xFFE91E63)
        UserRole.OPERATOR -> Color(0xFF2196F3)
        UserRole.VIEWER -> Color(0xFF4CAF50)
    }
}

private fun getRoleText(role: UserRole): String {
    return when (role) {
        UserRole.ADMIN -> "Admin"
        UserRole.OPERATOR -> "Operator"
        UserRole.VIEWER -> "Viewer"
    }
}