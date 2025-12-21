package com.example.s7opcuaapp.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.example.s7opcuaapp.util.StatusLockConfig
import com.example.s7opcuaapp.util.StatusLockConfig.StatusLockRule
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.launch
import javax.inject.Inject

data class StatusLockConfigUiState(
    val statusRules: Map<Int, StatusLockRule> = emptyMap(),
    val overrideActive: Boolean = false,
    val showResetConfirmation: Boolean = false,
    val editingStatus: Int? = null,
    val isLoading: Boolean = false,
    val errorMessage: String? = null
)

@HiltViewModel
class StatusLockConfigViewModel @Inject constructor(
    private val statusLockConfig: StatusLockConfig
) : ViewModel() {

    private val _uiState = MutableStateFlow(StatusLockConfigUiState())
    val uiState: StateFlow<StatusLockConfigUiState> = _uiState.asStateFlow()

    init {
        // Observe status rules
        viewModelScope.launch {
            combine(
                statusLockConfig.currentRules,
                statusLockConfig.overrideActive
            ) { rules, override ->
                StatusLockConfigUiState(
                    statusRules = rules,
                    overrideActive = override
                )
            }.collect { state ->
                _uiState.update { currentState ->
                    currentState.copy(
                        statusRules = state.statusRules,
                        overrideActive = state.overrideActive
                    )
                }
            }
        }
    }

    fun toggleRuleEnabled(statusValue: Int) {
        val currentRule = _uiState.value.statusRules[statusValue] ?: return
        statusLockConfig.setStatusLockEnabled(statusValue, !currentRule.isEnabled)
    }

    fun toggleOverride() {
        statusLockConfig.setOverrideActive(!_uiState.value.overrideActive)
    }

    fun showExemptButtonsDialog(statusValue: Int) {
        _uiState.update { it.copy(editingStatus = statusValue) }
    }

    fun hideExemptButtonsDialog() {
        _uiState.update { it.copy(editingStatus = null) }
    }

    fun toggleExemptButton(buttonIndex: Int) {
        val editingStatus = _uiState.value.editingStatus ?: return
        statusLockConfig.toggleExemptButton(editingStatus, buttonIndex)
    }

    fun showResetConfirmation() {
        _uiState.update { it.copy(showResetConfirmation = true) }
    }

    fun hideResetConfirmation() {
        _uiState.update { it.copy(showResetConfirmation = false) }
    }

    fun resetToDefaults() {
        statusLockConfig.resetToDefaults()
    }

    fun exportConfiguration() {
        viewModelScope.launch {
            try {
                val config = statusLockConfig.exportConfiguration()
                // Handle export (e.g., share or save to file)
                _uiState.update {
                    it.copy(errorMessage = "Export chức năng đang phát triển")
                }
            } catch (e: Exception) {
                _uiState.update {
                    it.copy(errorMessage = "Export thất bại: ${e.message}")
                }
            }
        }
    }

    fun importConfiguration(configString: String) {
        viewModelScope.launch {
            try {
                val success = statusLockConfig.importConfiguration(configString)
                if (!success) {
                    _uiState.update {
                        it.copy(errorMessage = "Import thất bại: Cấu hình không hợp lệ")
                    }
                }
            } catch (e: Exception) {
                _uiState.update {
                    it.copy(errorMessage = "Import thất bại: ${e.message}")
                }
            }
        }
    }

    fun clearError() {
        _uiState.update { it.copy(errorMessage = null) }
    }
}