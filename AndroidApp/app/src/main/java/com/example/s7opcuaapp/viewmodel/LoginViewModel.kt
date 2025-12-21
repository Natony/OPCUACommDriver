package com.example.s7opcuaapp.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.example.s7opcuaapp.data.repository.UserRepository
import com.example.s7opcuaapp.ui.screen.login.LoginUiState
import com.example.s7opcuaapp.util.SessionManager
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject

@HiltViewModel
class LoginViewModel @Inject constructor(
    private val userRepository: UserRepository,
    private val sessionManager: SessionManager
) : ViewModel() {

    private val _uiState = MutableStateFlow(LoginUiState())
    val uiState: StateFlow<LoginUiState> = _uiState

    fun onUsernameChanged(newUsername: String) {
        // Trim khoảng trắng khi user nhập
        _uiState.value = _uiState.value.copy(
            username = newUsername.trim(),
            errorMessage = null
        )
    }

    fun onPasswordChanged(newPassword: String) {
        _uiState.value = _uiState.value.copy(password = newPassword, errorMessage = null)
    }

    fun onTogglePasswordVisibility() {
        _uiState.value = _uiState.value.copy(
            isPasswordVisible = !_uiState.value.isPasswordVisible
        )
    }

    fun onLoginClicked(onSuccess: () -> Unit) {
        val current = _uiState.value

        // Trim lại một lần nữa để chắc chắn
        val username = current.username.trim()
        val password = current.password.trim()

        if (username.isBlank() || password.isBlank()) {
            _uiState.value = current.copy(errorMessage = "Vui lòng nhập đầy đủ thông tin")
            return
        }

        println("🔑 Login attempt:")
        println("   Username: '$username'")
        println("   Password: '$password'")

        _uiState.value = current.copy(isLoading = true, errorMessage = null)

        viewModelScope.launch {
            try {
                println("🔄 Calling userRepository.authenticate...")
                userRepository.authenticate(username, password) // Sử dụng username đã trim
                    .fold(
                        onSuccess = { user ->
                            println("✅ Authentication successful:")
                            println("   User: ${user.username}")
                            println("   Role: ${user.role}")
                            println("   Active: ${user.isActive}")

                            // Create session
                            val sessionId = sessionManager.login(user)
                            println("✅ Session created: $sessionId")

                            _uiState.value = _uiState.value.copy(isLoading = false)
                            onSuccess()
                        },
                        onFailure = { exception ->
                            println("❌ Authentication failed:")
                            println("   Error: ${exception.message}")
                            exception.printStackTrace()

                            _uiState.value = _uiState.value.copy(
                                isLoading = false,
                                errorMessage = exception.message ?: "Đăng nhập thất bại"
                            )
                        }
                    )
            } catch (e: Exception) {
                println("💥 Login error: ${e.message}")
                e.printStackTrace()
                _uiState.value = _uiState.value.copy(
                    isLoading = false,
                    errorMessage = "Lỗi hệ thống: ${e.message}"
                )
            }
        }
    }
}