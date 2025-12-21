package com.example.s7opcuaapp.data.local.dao

import androidx.room.*
import com.example.s7opcuaapp.data.model.DeviceAccessLog
import kotlinx.coroutines.flow.Flow

@Dao
interface DeviceAccessLogDao {
    @Query("SELECT * FROM device_access_logs ORDER BY timestamp DESC LIMIT :limit")
    fun getRecentLogs(limit: Int = 500): Flow<List<DeviceAccessLog>>

    @Query("SELECT * FROM device_access_logs WHERE userId = :userId ORDER BY timestamp DESC")
    fun getLogsByUser(userId: String): Flow<List<DeviceAccessLog>>

    @Query("SELECT * FROM device_access_logs WHERE deviceId = :deviceId ORDER BY timestamp DESC")
    fun getLogsByDevice(deviceId: String): Flow<List<DeviceAccessLog>>

    @Query("SELECT * FROM device_access_logs WHERE action = :actionName ORDER BY timestamp DESC")
    fun getLogsByAction(actionName: String): Flow<List<DeviceAccessLog>>

    @Query("SELECT * FROM device_access_logs WHERE timestamp >= :startTime AND timestamp <= :endTime ORDER BY timestamp DESC")
    fun getLogsByDateRange(startTime: Long, endTime: Long): Flow<List<DeviceAccessLog>>

    @Query(
        "SELECT * FROM device_access_logs " +
                "WHERE (:userId IS NULL OR userId = :userId) " +
                "AND (:deviceId IS NULL OR deviceId = :deviceId) " +
                "AND timestamp >= :startTime AND timestamp <= :endTime " +
                "ORDER BY timestamp DESC"
    )
    fun getFilteredLogs(
        userId: String?,
        deviceId: String?,
        startTime: Long,
        endTime: Long
    ): Flow<List<DeviceAccessLog>>

    @Insert
    suspend fun insertLog(log: DeviceAccessLog)

    @Insert
    suspend fun insertLogs(logs: List<DeviceAccessLog>)

    @Query("DELETE FROM device_access_logs WHERE timestamp < :beforeTime")
    suspend fun deleteOldLogs(beforeTime: Long)

    @Query("SELECT * FROM device_access_logs WHERE timestamp >= :startTime")
    suspend fun getLogsForStats(startTime: Long): List<DeviceAccessLog>

    @Query("SELECT COUNT(*) FROM device_access_logs")
    suspend fun getTotalLogCount(): Int

    @Query("SELECT COUNT(*) FROM device_access_logs WHERE userId = :userId")
    suspend fun getLogCountByUser(userId: String): Int

    @Query("SELECT COUNT(*) FROM device_access_logs WHERE deviceId = :deviceId")
    suspend fun getLogCountByDevice(deviceId: String): Int

    @Query("DELETE FROM device_access_logs")
    suspend fun deleteAllLogs()
}
