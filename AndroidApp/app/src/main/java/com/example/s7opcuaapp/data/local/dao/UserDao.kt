package com.example.s7opcuaapp.data.local.dao

import androidx.room.*
import com.example.s7opcuaapp.data.model.User
import com.example.s7opcuaapp.data.model.UserRole
import kotlinx.coroutines.flow.Flow

@Dao
interface UserDao {
    @Query("SELECT * FROM users ORDER BY username ASC")
    fun getAllUsers(): Flow<List<User>>

    @Query("SELECT * FROM users WHERE isActive = 1 ORDER BY username ASC")
    fun getAllActiveUsers(): Flow<List<User>>

    @Query("SELECT * FROM users WHERE id = :userId")
    suspend fun getUserById(userId: String): User?

    @Query("SELECT * FROM users WHERE id = :userId")
    fun getUserByIdFlow(userId: String): Flow<User?>

    @Query("SELECT * FROM users WHERE username = :username LIMIT 1")
    suspend fun getUserByUsername(username: String): User?

    @Query("SELECT * FROM users WHERE role = :role ORDER BY username ASC")
    fun getUsersByRole(role: UserRole): Flow<List<User>>

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertUser(user: User)

    @Insert(onConflict = OnConflictStrategy.REPLACE)
    suspend fun insertUsers(users: List<User>)

    @Update
    suspend fun updateUser(user: User)

    @Delete
    suspend fun deleteUser(user: User)

    @Query("UPDATE users SET lastLoginTime = :timestamp WHERE id = :userId")
    suspend fun updateLastLogin(userId: String, timestamp: Long)

    @Query("""
        UPDATE users 
        SET isActive = :isActive, 
            modifiedAt = :modifiedAt, 
            modifiedBy = :modifiedBy 
        WHERE id = :userId
    """)
    suspend fun updateUserStatus(
        userId: String,
        isActive: Boolean,
        modifiedAt: Long,
        modifiedBy: String
    )

    @Query("SELECT COUNT(*) FROM users")
    suspend fun getTotalUserCount(): Int

    @Query("SELECT COUNT(*) FROM users WHERE role = :role AND isActive = 1")
    suspend fun getActiveUserCountByRole(role: UserRole): Int

    @Query("DELETE FROM users")
    suspend fun deleteAllUsers()
}