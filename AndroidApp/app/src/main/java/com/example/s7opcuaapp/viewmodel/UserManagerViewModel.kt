package com.example.s7opcuaapp.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.example.s7opcuaapp.data.model.User
import com.example.s7opcuaapp.data.model.UserRole
import com.example.s7opcuaapp.data.repository.UserRepository
import com.example.s7opcuaapp.util.PasswordUtils
import com.example.s7opcuaapp.util.SessionManager
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import javax.inject.Inject

data class UserManagerUiState(
    val userList: List<User> = emptyList(),
    val isLoading: Boolean = false,
    val errorMessage: String? = null,
    val successMessage: String? = null,

    // Add/Edit user dialog
    val showAddEditDialog: Boolean = false,
    val editingUser: User? = null,
    val dialogUsername: String = "",
    val dialogPassword: String = "",
    val dialogConfirmPassword: String = "",
    val dialogRole: UserRole = UserRole.VIEWER,
    val dialogPasswordStrength: PasswordUtils.PasswordStrength? = null,

    // Change password dialog
    val showChangePasswordDialog: Boolean = false,
    val changingPasswordForUser: User? = null,
    val newPassword: String = "",
    val confirmNewPassword: String = "",
    val newPasswordStrength: PasswordUtils.PasswordStrength? = null,

    // Delete confirmation
    val showDeleteConfirmation: Boolean = false,
    val userToDelete: User? = null,

    // Stats
    val totalUsers: Int = 0,
    val activeUsers: Int = 0,
    val adminCount: Int = 0,
    val operatorCount: Int = 0,
    val viewerCount: Int = 0
)

@HiltViewModel
class UserManagerViewModel @Inject constructor(
    private val userRepository: UserRepository,
    private val sessionManager: SessionManager
) : ViewModel() {

    private val _uiState = MutableStateFlow(UserManagerUiState())
    val uiState: StateFlow<UserManagerUiState> = _uiState.asStateFlow()

    init {
        loadUsers()
        loadStats()
    }

    private fun loadUsers() {
        viewModelScope.launch {
            userRepository.getAllUsers().collect { users ->
                _uiState.update {
                    it.copy(
                        userList = users.sortedBy { user -> user.username },
                        totalUsers = users.size,
                        activeUsers = users.count { user -> user.isActive }
                    )
                }
            }
        }
    }

    private fun loadStats() {
        viewModelScope.launch {
            val adminCount = userRepository.getActiveUserCountByRole(UserRole.ADMIN)
            val operatorCount = userRepository.getActiveUserCountByRole(UserRole.OPERATOR)
            val viewerCount = userRepository.getActiveUserCountByRole(UserRole.VIEWER)

            _uiState.update {
                it.copy(
                    adminCount = adminCount,
                    operatorCount = operatorCount,
                    viewerCount = viewerCount
                )
            }
        }
    }

    // Add/Edit User Dialog
    fun showAddUserDialog() {
        _uiState.update {
            it.copy(
                showAddEditDialog = true,
                editingUser = null,
                dialogUsername = "",
                dialogPassword = "",
                dialogConfirmPassword = "",
                dialogRole = UserRole.VIEWER,
                dialogPasswordStrength = null
            )
        }
    }

    fun showEditUserDialog(user: User) {
        _uiState.update {
            it.copy(
                showAddEditDialog = true,
                editingUser = user,
                dialogUsername = user.username,
                dialogPassword = "",
                dialogConfirmPassword = "",
                dialogRole = user.role,
                dialogPasswordStrength = null
            )
        }
    }

    fun hideAddEditDialog() {
        _uiState.update {
            it.copy(
                showAddEditDialog = false,
                editingUser = null,
                errorMessage = null
            )
        }
    }

    fun updateDialogUsername(username: String) {
        _uiState.update { it.copy(dialogUsername = username) }
    }

    fun updateDialogPassword(password: String) {
        val strength = if (password.isNotEmpty()) {
            PasswordUtils.getPasswordStrength(password)
        } else null

        _uiState.update {
            it.copy(
                dialogPassword = password,
                dialogPasswordStrength = strength
            )
        }
    }

    fun updateDialogConfirmPassword(password: String) {
        _uiState.update { it.copy(dialogConfirmPassword = password) }
    }

    fun updateDialogRole(role: UserRole) {
        _uiState.update { it.copy(dialogRole = role) }
    }

    fun saveUser() {
        val state = _uiState.value
        val currentUser = sessionManager.getCurrentUser() ?: return

        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true, errorMessage = null) }

            try {
                if (state.editingUser == null) {
                    // Create new user
                    if (state.dialogPassword != state.dialogConfirmPassword) {
                        throw Exception("Mật khẩu không khớp")
                    }

                    userRepository.createUser(
                        username = state.dialogUsername,
                        password = state.dialogPassword,
                        role = state.dialogRole,
                        createdBy = currentUser.username
                    ).fold(
                        onSuccess = {
                            _uiState.update {
                                it.copy(
                                    successMessage = "Tạo người dùng thành công",
                                    showAddEditDialog = false,
                                    isLoading = false
                                )
                            }
                            loadStats()
                        },
                        onFailure = { error ->
                            _uiState.update {
                                it.copy(
                                    errorMessage = error.message,
                                    isLoading = false
                                )
                            }
                        }
                    )
                } else {
                    // Update existing user
                    val updatedUser = state.editingUser.copy(
                        role = state.dialogRole,
                        modifiedBy = currentUser.username,
                        modifiedAt = System.currentTimeMillis()
                    )

                    userRepository.updateUser(updatedUser).fold(
                        onSuccess = {
                            _uiState.update {
                                it.copy(
                                    successMessage = "Cập nhật người dùng thành công",
                                    showAddEditDialog = false,
                                    isLoading = false
                                )
                            }
                            loadStats()
                        },
                        onFailure = { error ->
                            _uiState.update {
                                it.copy(
                                    errorMessage = error.message,
                                    isLoading = false
                                )
                            }
                        }
                    )
                }
            } catch (e: Exception) {
                _uiState.update {
                    it.copy(
                        errorMessage = e.message,
                        isLoading = false
                    )
                }
            }
        }
    }

    // Change Password Dialog
    fun showChangePasswordDialog(user: User) {
        _uiState.update {
            it.copy(
                showChangePasswordDialog = true,
                changingPasswordForUser = user,
                newPassword = "",
                confirmNewPassword = "",
                newPasswordStrength = null
            )
        }
    }

    fun hideChangePasswordDialog() {
        _uiState.update {
            it.copy(
                showChangePasswordDialog = false,
                changingPasswordForUser = null,
                errorMessage = null
            )
        }
    }

    fun updateNewPassword(password: String) {
        val strength = if (password.isNotEmpty()) {
            PasswordUtils.getPasswordStrength(password)
        } else null

        _uiState.update {
            it.copy(
                newPassword = password,
                newPasswordStrength = strength
            )
        }
    }

    fun updateConfirmNewPassword(password: String) {
        _uiState.update { it.copy(confirmNewPassword = password) }
    }

    fun changePassword() {
        val state = _uiState.value
        val currentUser = sessionManager.getCurrentUser() ?: return
        val targetUser = state.changingPasswordForUser ?: return

        if (state.newPassword != state.confirmNewPassword) {
            _uiState.update { it.copy(errorMessage = "Mật khẩu không khớp") }
            return
        }

        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true, errorMessage = null) }

            userRepository.changePassword(
                userId = targetUser.id,
                newPassword = state.newPassword,
                modifiedBy = currentUser.username
            ).fold(
                onSuccess = {
                    _uiState.update {
                        it.copy(
                            successMessage = "Đổi mật khẩu thành công",
                            showChangePasswordDialog = false,
                            isLoading = false
                        )
                    }
                },
                onFailure = { error ->
                    _uiState.update {
                        it.copy(
                            errorMessage = error.message,
                            isLoading = false
                        )
                    }
                }
            )
        }
    }

    // Activate/Deactivate User
    fun toggleUserStatus(user: User) {
        val currentUser = sessionManager.getCurrentUser() ?: return

        viewModelScope.launch {
            val result = if (user.isActive) {
                userRepository.deactivateUser(user.id, currentUser.username)
            } else {
                userRepository.activateUser(user.id, currentUser.username)
            }

            result.fold(
                onSuccess = {
                    val action = if (user.isActive) "vô hiệu hóa" else "kích hoạt"
                    _uiState.update {
                        it.copy(successMessage = "Đã $action người dùng ${user.username}")
                    }
                    loadStats()
                },
                onFailure = { error ->
                    _uiState.update { it.copy(errorMessage = error.message) }
                }
            )
        }
    }

    // Delete User
    fun showDeleteConfirmation(user: User) {
        _uiState.update {
            it.copy(
                showDeleteConfirmation = true,
                userToDelete = user
            )
        }
    }

    fun hideDeleteConfirmation() {
        _uiState.update {
            it.copy(
                showDeleteConfirmation = false,
                userToDelete = null
            )
        }
    }

    fun confirmDeleteUser() {
        val user = _uiState.value.userToDelete ?: return

        viewModelScope.launch {
            _uiState.update { it.copy(isLoading = true) }

            userRepository.deleteUser(user.id).fold(
                onSuccess = {
                    _uiState.update {
                        it.copy(
                            successMessage = "Đã xóa người dùng ${user.username}",
                            showDeleteConfirmation = false,
                            userToDelete = null,
                            isLoading = false
                        )
                    }
                    loadStats()
                },
                onFailure = { error ->
                    _uiState.update {
                        it.copy(
                            errorMessage = error.message,
                            isLoading = false
                        )
                    }
                }
            )
        }
    }

    fun clearMessages() {
        _uiState.update {
            it.copy(
                errorMessage = null,
                successMessage = null
            )
        }
    }
}