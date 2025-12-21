package com.example.s7opcuaapp.data.repository

import com.example.s7opcuaapp.data.model.*
import kotlinx.coroutines.flow.Flow
import java.util.Date

interface LogRepository {
    // Login History
    fun getAllLoginHistory(): Flow<List<LoginHistory>>
    fun getLoginHistoryByUser(userId: String): Flow<List<LoginHistory>>
    fun getLoginHistoryByDateRange(startDate: Date, endDate: Date): Flow<List<LoginHistory>>
    suspend fun logLogout(historyId: String, logoutTime: Long, status: LoginStatus)

    // Device Access Logs
    fun getRecentDeviceLogs(limit: Int = 500): Flow<List<DeviceAccessLog>>
    fun getDeviceLogsByUser(userId: String): Flow<List<DeviceAccessLog>>
    fun getDeviceLogsByDevice(deviceId: String): Flow<List<DeviceAccessLog>>
    fun getDeviceLogsByAction(action: DeviceAction): Flow<List<DeviceAccessLog>>
    fun getDeviceLogsByDateRange(startDate: Date, endDate: Date): Flow<List<DeviceAccessLog>>
    suspend fun logDeviceAccess(
        user: User,
        device: DeviceEntity,
        action: DeviceAction,
        details: ActionDetail? = null,
        success: Boolean = true,
        errorMessage: String? = null
    )

    // Statistics
    suspend fun getLoginStats(sinceDate: Date): LoginStats
    suspend fun getDeviceActionStats(sinceDate: Date): List<ActionStat>
    suspend fun getDeviceAccessStats(sinceDate: Date): List<DeviceAccessStat>

    // Maintenance
    suspend fun cleanupOldLogs(beforeDate: Date)
}

// Data classes cho statistics
data class ActionStat(
    val action: DeviceAction,
    val count: Int
)

data class DeviceAccessStat(
    val deviceId: String,
    val deviceName: String,
    val accessCount: Int
)

data class LoginStats(
    val activeUsers: Int,
    val totalLogins: Int,
    val successfulLogins: Int
)