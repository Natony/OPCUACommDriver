package com.example.s7opcuaapp.data.repository

import com.example.s7opcuaapp.data.local.AppDatabase
import com.example.s7opcuaapp.data.model.*
import kotlinx.coroutines.flow.Flow
import java.util.Date
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class LogRepositoryImpl @Inject constructor(
    private val database: AppDatabase
) : LogRepository {

    private val loginHistoryDao = database.loginHistoryDao()
    private val deviceAccessLogDao = database.deviceAccessLogDao()

    override fun getAllLoginHistory(): Flow<List<LoginHistory>> =
        loginHistoryDao.getAllLoginHistory()

    override fun getLoginHistoryByUser(userId: String): Flow<List<LoginHistory>> =
        loginHistoryDao.getLoginHistoryByUser(userId)

    override fun getLoginHistoryByDateRange(startDate: Date, endDate: Date): Flow<List<LoginHistory>> =
        loginHistoryDao.getLoginHistoryByDateRange(startDate.time, endDate.time)

    override suspend fun logLogout(historyId: String, logoutTime: Long, status: LoginStatus) {
        loginHistoryDao.updateLogout(historyId, logoutTime, status)
    }

    override fun getRecentDeviceLogs(limit: Int): Flow<List<DeviceAccessLog>> =
        deviceAccessLogDao.getRecentLogs(limit)

    override fun getDeviceLogsByUser(userId: String): Flow<List<DeviceAccessLog>> =
        deviceAccessLogDao.getLogsByUser(userId)

    override fun getDeviceLogsByDevice(deviceId: String): Flow<List<DeviceAccessLog>> =
        deviceAccessLogDao.getLogsByDevice(deviceId)

    override fun getDeviceLogsByAction(action: DeviceAction): Flow<List<DeviceAccessLog>> =
        // Use DAO method accepting DeviceAction
        deviceAccessLogDao.getLogsByAction(action.name)

    override fun getDeviceLogsByDateRange(startDate: Date, endDate: Date): Flow<List<DeviceAccessLog>> =
        deviceAccessLogDao.getLogsByDateRange(startDate.time, endDate.time)

    override suspend fun logDeviceAccess(
        user: User,
        device: DeviceEntity,
        action: DeviceAction,
        details: ActionDetail?,
        success: Boolean,
        errorMessage: String?
    ) {
        // Auto-generate ID by omitting it to use Room's @PrimaryKey(autoGenerate = true)
        val log = DeviceAccessLog(
            userId = user.id,
            username = user.username,
            deviceId = device.id,
            deviceName = device.name,
            action = action.name,
            details = details?.let { ActionDetail.toJson(it) },
            timestamp = System.currentTimeMillis(),
            success = success,
            errorMessage = errorMessage
        )
        deviceAccessLogDao.insertLog(log)
    }

    override suspend fun getLoginStats(sinceDate: Date): LoginStats =
        try {
            loginHistoryDao.getLoginStats(sinceDate.time)
        } catch (e: Exception) {
            // Fallback
            LoginStats(activeUsers = 0, totalLogins = 0, successfulLogins = 0)
        }

    override suspend fun getDeviceActionStats(sinceDate: Date): List<ActionStat> {
        return try {
            val logs = deviceAccessLogDao.getLogsForStats(sinceDate.time)
            logs.groupBy { it.action }
                .map { (actionStr, logsList) ->
                    ActionStat(action = DeviceAction.valueOf(actionStr), count = logsList.size)
                }
        } catch (e: Exception) {
            emptyList()
        }
    }

    override suspend fun getDeviceAccessStats(sinceDate: Date): List<DeviceAccessStat> {
        return try {
            val logs = deviceAccessLogDao.getLogsForStats(sinceDate.time)
            logs.groupBy { "${it.deviceId}|${it.deviceName}" }
                .map { (key, logsList) ->
                    val (deviceId, deviceName) = key.split("|")
                    DeviceAccessStat(deviceId, deviceName, logsList.size)
                }
                .sortedByDescending { it.accessCount }
        } catch (e: Exception) {
            emptyList()
        }
    }

    override suspend fun cleanupOldLogs(beforeDate: Date) {
        try {
            loginHistoryDao.deleteOldHistory(beforeDate.time)
            deviceAccessLogDao.deleteOldLogs(beforeDate.time)
        } catch (_: Exception) {
            // ignore
        }
    }
}
