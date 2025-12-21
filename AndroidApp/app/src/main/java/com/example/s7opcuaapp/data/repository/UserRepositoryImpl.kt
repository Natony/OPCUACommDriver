package com.example.s7opcuaapp.data.repository

import android.util.Log
import com.example.s7opcuaapp.data.local.AppDatabase
import com.example.s7opcuaapp.data.local.PrefsManager
import com.example.s7opcuaapp.data.model.*
import com.example.s7opcuaapp.util.PasswordUtils
import kotlinx.coroutines.flow.Flow
import java.util.UUID
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class UserRepositoryImpl @Inject constructor(
    private val database: AppDatabase,
    private val prefsManager: PrefsManager
) : UserRepository {

    private val userDao = database.userDao()
    private val loginHistoryDao = database.loginHistoryDao()

    // In-memory session storage (trong production nên dùng Redis/persistent storage)
    private val sessions = mutableMapOf<String, Session>()

    override suspend fun createUser(
        username: String,
        password: String,
        role: UserRole,
        createdBy: String
    ): Result<User> {
        return try {
            // Check if username exists
            if (getUserByUsername(username) != null) {
                return Result.failure(Exception("Username already exists"))
            }

            // Validate password
            if (!PasswordUtils.isValidPassword(password)) {
                return Result.failure(Exception("Password does not meet requirements"))
            }

            val user = User(
                id = UUID.randomUUID().toString(),
                username = username,
                passwordHash = PasswordUtils.hashPassword(password),
                role = role,
                isActive = true,
                createdAt = System.currentTimeMillis(),
                createdBy = createdBy
            )

            userDao.insertUser(user)
            Result.success(user)
        } catch (e: Exception) {
            Log.e("UserRepository", "Error creating user", e)
            Result.failure(e)
        }
    }

    override suspend fun updateUser(user: User): Result<Unit> {
        return try {
            userDao.updateUser(user.copy(modifiedAt = System.currentTimeMillis()))
            Result.success(Unit)
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    override suspend fun deleteUser(userId: String): Result<Unit> {
        return try {
            val user = userDao.getUserById(userId) ?: return Result.failure(Exception("User not found"))

            // Don't delete admin if it's the last one
            if (user.role == UserRole.ADMIN) {
                val adminCount = userDao.getActiveUserCountByRole(UserRole.ADMIN)
                if (adminCount <= 1) {
                    return Result.failure(Exception("Cannot delete the last admin"))
                }
            }

            userDao.deleteUser(user)
            Result.success(Unit)
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    override suspend fun activateUser(userId: String, modifiedBy: String): Result<Unit> {
        return try {
            userDao.updateUserStatus(userId, true, System.currentTimeMillis(), modifiedBy)
            Result.success(Unit)
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    override suspend fun deactivateUser(userId: String, modifiedBy: String): Result<Unit> {
        return try {
            // Don't deactivate last admin
            val user = userDao.getUserById(userId) ?: return Result.failure(Exception("User not found"))
            if (user.role == UserRole.ADMIN) {
                val adminCount = userDao.getActiveUserCountByRole(UserRole.ADMIN)
                if (adminCount <= 1) {
                    return Result.failure(Exception("Cannot deactivate the last admin"))
                }
            }

            userDao.updateUserStatus(userId, false, System.currentTimeMillis(), modifiedBy)
            // End all sessions for this user
            sessions.entries.removeIf { it.value.userId == userId }
            Result.success(Unit)
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    override suspend fun changePassword(userId: String, newPassword: String, modifiedBy: String): Result<Unit> {
        return try {
            if (!PasswordUtils.isValidPassword(newPassword)) {
                return Result.failure(Exception("Password does not meet requirements"))
            }

            val user = userDao.getUserById(userId) ?: return Result.failure(Exception("User not found"))
            val updatedUser = user.copy(
                passwordHash = PasswordUtils.hashPassword(newPassword),
                modifiedAt = System.currentTimeMillis(),
                modifiedBy = modifiedBy
            )
            userDao.updateUser(updatedUser)

            // End all sessions for this user
            sessions.entries.removeIf { it.value.userId == userId }
            Result.success(Unit)
        } catch (e: Exception) {
            Result.failure(e)
        }
    }

    override fun getAllUsers(): Flow<List<User>> = userDao.getAllUsers()

    override fun getActiveUsers(): Flow<List<User>> = userDao.getAllActiveUsers()

    override suspend fun getUserById(userId: String): User? = userDao.getUserById(userId)

    override suspend fun getUserByUsername(username: String): User? = userDao.getUserByUsername(username)

    override fun getUsersByRole(role: UserRole): Flow<List<User>> = userDao.getUsersByRole(role)

    override suspend fun authenticate(username: String, password: String): Result<User> {
        return try {
            val historyId = UUID.randomUUID().toString()
            val loginTime = System.currentTimeMillis()

            println("🔍 Authenticating user: '$username'") // Thêm quotes để thấy spaces
            val user = userDao.getUserByUsername(username)

            if (user == null) {
                println("❌ User not found: '$username'")
                // KHÔNG insert LoginHistory với userId="unknown" vì sẽ gây foreign key error
                // Chỉ log và return error
                return Result.failure(Exception("Invalid credentials"))
            }

            println("✅ User found: ${user.username}, active: ${user.isActive}")

            if (!user.isActive) {
                println("❌ User is inactive: $username")
                loginHistoryDao.insertLoginHistory(
                    LoginHistory(
                        id = historyId,
                        userId = user.id, // Sử dụng user.id thực tế
                        username = username,
                        loginTime = loginTime,
                        loginStatus = LoginStatus.FAILED_ACCOUNT_DISABLED
                    )
                )
                return Result.failure(Exception("Account is disabled"))
            }

            // Debug password verification
            val inputHash = PasswordUtils.hashPassword(password)
            val storedHash = user.passwordHash
            println("🔐 Password verification:")
            println("   Input password: '$password'")
            println("   Input hash: $inputHash")
            println("   Stored hash: $storedHash")
            println("   Match: ${inputHash == storedHash}")

            if (!PasswordUtils.verifyPassword(password, user.passwordHash)) {
                println("❌ Password verification failed for user: $username")
                loginHistoryDao.insertLoginHistory(
                    LoginHistory(
                        id = historyId,
                        userId = user.id, // Sử dụng user.id thực tế
                        username = username,
                        loginTime = loginTime,
                        loginStatus = LoginStatus.FAILED_INVALID_CREDENTIALS
                    )
                )
                return Result.failure(Exception("Invalid credentials"))
            }

            // Success
            println("✅ Authentication successful for user: $username")
            userDao.updateLastLogin(user.id, loginTime)
            loginHistoryDao.insertLoginHistory(
                LoginHistory(
                    id = historyId,
                    userId = user.id,
                    username = username,
                    loginTime = loginTime,
                    loginStatus = LoginStatus.SUCCESS,
                    appVersion = "1.0.0"
                )
            )

            Result.success(user)
        } catch (e: Exception) {
            println("💥 Authentication error: ${e.message}")
            e.printStackTrace()
            Result.failure(e)
        }
    }

    override suspend fun createSession(user: User, deviceInfo: String?): Session {
        val sessionId = UUID.randomUUID().toString()
        val now = System.currentTimeMillis()
        val session = Session(
            sessionId = sessionId,
            userId = user.id,
            user = user,
            loginTime = now,
            lastActivityTime = now,
            expiryTime = now + (8 * 60 * 60 * 1000), // 8 hours
            deviceInfo = deviceInfo
        )
        sessions[sessionId] = session
        return session
    }

    override suspend fun validateSession(sessionId: String): Session? {
        val session = sessions[sessionId] ?: return null
        val now = System.currentTimeMillis()

        if (now > session.expiryTime) {
            sessions.remove(sessionId)
            return null
        }

        // Update last activity
        val updatedSession = session.copy(lastActivityTime = now)
        sessions[sessionId] = updatedSession
        return updatedSession
    }

    override suspend fun endSession(sessionId: String) {
        sessions.remove(sessionId)
    }

    override suspend fun getActiveUserCount(): Int {
        return userDao.getAllUsers().let { flow ->
            var count = 0
            flow.collect { users ->
                count = users.count { it.isActive }
            }
            count
        }
    }

    override suspend fun getActiveUserCountByRole(role: UserRole): Int {
        return userDao.getActiveUserCountByRole(role)
    }
}