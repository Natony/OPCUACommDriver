package com.example.s7opcuaapp.viewmodel

import androidx.lifecycle.ViewModel
import com.example.s7opcuaapp.data.model.User
import com.example.s7opcuaapp.util.SessionManager
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.StateFlow
import javax.inject.Inject

@HiltViewModel
class HomeViewModel @Inject constructor(
    val sessionManager: SessionManager
) : ViewModel() {

    val currentUser: StateFlow<User?> = sessionManager.currentUser

    fun isAdmin(): Boolean = sessionManager.hasRole(com.example.s7opcuaapp.data.model.UserRole.ADMIN)

    fun canModifyDevices(): Boolean = sessionManager.canModifyDevices()
}