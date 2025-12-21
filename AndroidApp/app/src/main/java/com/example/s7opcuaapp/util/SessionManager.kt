package com.example.s7opcuaapp.util

import com.example.s7opcuaapp.data.local.PrefsManager
import com.example.s7opcuaapp.data.model.User
import com.example.s7opcuaapp.data.model.UserRole
import com.example.s7opcuaapp.data.repository.UserRepository
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.asStateFlow
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class SessionManager @Inject constructor(
    private val userRepository: UserRepository,
    private val prefsManager: PrefsManager
) {
    private val _currentUser = MutableStateFlow<User?>(null)
    val currentUser = _currentUser.asStateFlow()

    private var sessionId: String? = null

    suspend fun login(user: User): String {
        val session = userRepository.createSession(user)
        sessionId = session.sessionId
        _currentUser.value = user

        prefsManager.saveSession(
            sessionId = session.sessionId,
            userId = user.id,
            username = user.username,
            role = user.role.name
        )

        return session.sessionId
    }

    suspend fun logout() {
        sessionId?.let {
            userRepository.endSession(it)
        }
        sessionId = null
        _currentUser.value = null
        prefsManager.clearSession()
    }

    suspend fun validateSession(): Boolean {
        val savedSessionId = prefsManager.getSessionId() ?: return false
        val session = userRepository.validateSession(savedSessionId)
        return if (session != null) {
            sessionId = savedSessionId
            _currentUser.value = session.user
            true
        } else {
            logout()
            false
        }
    }

    fun getCurrentUser(): User? = _currentUser.value

    fun isLoggedIn(): Boolean = _currentUser.value != null

    fun hasRole(role: UserRole): Boolean {
        return _currentUser.value?.role == role
    }

    fun canModifyDevices(): Boolean {
        return _currentUser.value?.canModifyDevices() ?: false
    }
}