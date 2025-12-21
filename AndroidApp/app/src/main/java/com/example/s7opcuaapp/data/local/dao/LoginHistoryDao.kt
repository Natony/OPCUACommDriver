package com.example.s7opcuaapp.data.local.dao

import androidx.room.*
import com.example.s7opcuaapp.data.model.LoginHistory
import com.example.s7opcuaapp.data.model.LoginStatus
import com.example.s7opcuaapp.data.repository.LoginStats
import kotlinx.coroutines.flow.Flow

@Dao
interface LoginHistoryDao {
    @Query("SELECT * FROM login_history ORDER BY loginTime DESC")
    fun getAllLoginHistory(): Flow<List<LoginHistory>>

    @Query("SELECT * FROM login_history WHERE userId = :userId ORDER BY loginTime DESC")
    fun getLoginHistoryByUser(userId: String): Flow<List<LoginHistory>>

    @Query("""
        SELECT * FROM login_history 
        WHERE loginTime >= :startTime AND loginTime <= :endTime 
        ORDER BY loginTime DESC
    """)
    fun getLoginHistoryByDateRange(startTime: Long, endTime: Long): Flow<List<LoginHistory>>

    @Query("""
        SELECT * FROM login_history 
        WHERE loginStatus = :status 
        ORDER BY loginTime DESC 
        LIMIT :limit
    """)
    suspend fun getRecentLoginsByStatus(status: LoginStatus, limit: Int = 100): List<LoginHistory>

    @Insert
    suspend fun insertLoginHistory(history: LoginHistory)

    @Query("UPDATE login_history SET logoutTime = :logoutTime, loginStatus = :status WHERE id = :historyId")
    suspend fun updateLogout(historyId: String, logoutTime: Long, status: LoginStatus)

    @Query("DELETE FROM login_history WHERE loginTime < :beforeTime")
    suspend fun deleteOldHistory(beforeTime: Long)

    // FIX: Đơn giản hóa query LoginStats
    @Query("""
        SELECT 
            COUNT(DISTINCT userId) as activeUsers, 
            COUNT(*) as totalLogins,
            COUNT(CASE WHEN loginStatus = 'SUCCESS' THEN 1 END) as successfulLogins
        FROM login_history 
        WHERE loginTime >= :startTime
    """)
    suspend fun getLoginStats(startTime: Long): LoginStats
}