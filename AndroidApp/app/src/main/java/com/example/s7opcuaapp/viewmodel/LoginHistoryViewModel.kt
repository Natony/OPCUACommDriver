package com.example.s7opcuaapp.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.example.s7opcuaapp.data.model.*
import com.example.s7opcuaapp.data.repository.LogRepository
import com.example.s7opcuaapp.data.repository.UserRepository
import com.example.s7opcuaapp.util.DateUtils
import com.example.s7opcuaapp.util.SessionManager
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.launch
import java.util.*
import javax.inject.Inject

data class LoginHistoryUiState(
    val loginHistoryList: List<LoginHistoryItem> = emptyList(),
    val isLoading: Boolean = false,
    val errorMessage: String? = null,

    // Filters
    val selectedUser: User? = null,
    val selectedDateRange: DateRange = DateRange.TODAY,
    val showOnlyFailures: Boolean = false,

    // Stats
    val totalLogins: Int = 0,
    val successfulLogins: Int = 0,
    val failedLogins: Int = 0,
    val activeUsers: Int = 0,

    // User list for filter
    val userList: List<User> = emptyList()
)

data class LoginHistoryItem(
    val history: LoginHistory,
    val durationText: String,
    val loginTimeText: String,
    val statusText: String,
    val statusColor: Long // Color as Long for Compose
)

enum class DateRange {
    TODAY, YESTERDAY, LAST_WEEK, LAST_MONTH, CUSTOM
}

@HiltViewModel
class LoginHistoryViewModel @Inject constructor(
    private val logRepository: LogRepository,
    private val userRepository: UserRepository,
    private val sessionManager: SessionManager
) : ViewModel() {

    private val _uiState = MutableStateFlow(LoginHistoryUiState())
    val uiState: StateFlow<LoginHistoryUiState> = _uiState.asStateFlow()

    private val _dateRange = MutableStateFlow(DateRange.TODAY)
    private val _selectedUser = MutableStateFlow<User?>(null)
    private val _showOnlyFailures = MutableStateFlow(false)

    init {
        loadUsers()
        observeLoginHistory()
        loadStats()
    }

    private fun loadUsers() {
        viewModelScope.launch {
            userRepository.getAllUsers().collect { users ->
                _uiState.update { it.copy(userList = users) }
            }
        }
    }

    private fun observeLoginHistory() {
        viewModelScope.launch {
            combine(
                _dateRange,
                _selectedUser,
                _showOnlyFailures
            ) { dateRange, user, showFailures ->
                Triple(dateRange, user, showFailures)
            }.flatMapLatest { (dateRange, user, showFailures) ->
                val (startDate, endDate) = getDateRangeValues(dateRange)

                when {
                    user != null -> logRepository.getLoginHistoryByUser(user.id)
                    else -> logRepository.getLoginHistoryByDateRange(startDate, endDate)
                }.map { histories ->
                    histories
                        .filter { !showFailures || it.loginStatus != LoginStatus.SUCCESS }
                        .map { history ->
                            LoginHistoryItem(
                                history = history,
                                durationText = DateUtils.formatDuration(
                                    history.loginTime,
                                    history.logoutTime
                                ),
                                loginTimeText = DateUtils.formatDateTime(history.loginTime),
                                statusText = getStatusText(history.loginStatus),
                                statusColor = getStatusColor(history.loginStatus)
                            )
                        }
                }
            }.catch { e ->
                _uiState.update { it.copy(errorMessage = e.message) }
            }.collect { items ->
                _uiState.update { it.copy(loginHistoryList = items) }
            }
        }
    }

    private fun loadStats() {
        viewModelScope.launch {
            val startDate = DateUtils.getStartOfDay()
            val stats = logRepository.getLoginStats(startDate)

            _uiState.update {
                it.copy(
                    totalLogins = stats.totalLogins,
                    successfulLogins = stats.successfulLogins,
                    failedLogins = stats.totalLogins - stats.successfulLogins,
                    activeUsers = stats.activeUsers
                )
            }
        }
    }

    fun selectDateRange(dateRange: DateRange) {
        _dateRange.value = dateRange
        _uiState.update { it.copy(selectedDateRange = dateRange) }
    }

    fun selectUser(user: User?) {
        _selectedUser.value = user
        _uiState.update { it.copy(selectedUser = user) }
    }

    fun toggleShowOnlyFailures() {
        _showOnlyFailures.value = !_showOnlyFailures.value
        _uiState.update { it.copy(showOnlyFailures = _showOnlyFailures.value) }
    }

    fun exportToCSV() {
        // TODO: Implement CSV export
        viewModelScope.launch {
            _uiState.update { it.copy(errorMessage = "Export feature coming soon") }
        }
    }

    private fun getDateRangeValues(dateRange: DateRange): Pair<Date, Date> {
        val endDate = Date()
        val startDate = when (dateRange) {
            DateRange.TODAY -> DateUtils.getStartOfDay()
            DateRange.YESTERDAY -> {
                val cal = Calendar.getInstance()
                cal.add(Calendar.DAY_OF_YEAR, -1)
                DateUtils.getStartOfDay(cal.time)
            }
            DateRange.LAST_WEEK -> {
                val cal = Calendar.getInstance()
                cal.add(Calendar.DAY_OF_YEAR, -7)
                cal.time
            }
            DateRange.LAST_MONTH -> {
                val cal = Calendar.getInstance()
                cal.add(Calendar.MONTH, -1)
                cal.time
            }
            DateRange.CUSTOM -> DateUtils.getStartOfDay() // TODO: Implement custom date picker
        }
        return Pair(startDate, endDate)
    }

    private fun getStatusText(status: LoginStatus): String {
        return when (status) {
            LoginStatus.SUCCESS -> "Thành công"
            LoginStatus.FAILED_INVALID_CREDENTIALS -> "Sai mật khẩu"
            LoginStatus.FAILED_ACCOUNT_DISABLED -> "Tài khoản bị khóa"
            LoginStatus.FAILED_UNKNOWN -> "Lỗi không xác định"
            LoginStatus.SESSION_TIMEOUT -> "Hết phiên"
            LoginStatus.MANUAL_LOGOUT -> "Đăng xuất"
        }
    }

    private fun getStatusColor(status: LoginStatus): Long {
        return when (status) {
            LoginStatus.SUCCESS -> 0xFF4CAF50 // Green
            LoginStatus.MANUAL_LOGOUT -> 0xFF2196F3 // Blue
            LoginStatus.SESSION_TIMEOUT -> 0xFFFF9800 // Orange
            else -> 0xFFF44336 // Red
        }
    }
}
