package com.example.s7opcuaapp.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.example.s7opcuaapp.util.SessionManager
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.launch
import javax.inject.Inject

@HiltViewModel
class LogoutViewModel @Inject constructor(
    private val sessionManager: SessionManager
) : ViewModel() {

    fun logout(onComplete: () -> Unit) {
        viewModelScope.launch {
            sessionManager.logout()
            onComplete()
        }
    }
}