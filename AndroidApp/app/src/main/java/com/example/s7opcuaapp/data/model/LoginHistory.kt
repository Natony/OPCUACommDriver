package com.example.s7opcuaapp.data.model

import androidx.room.*

@Entity(
    tableName = "login_history",
    foreignKeys = [
        ForeignKey(
            entity = User::class,
            parentColumns = ["id"],
            childColumns = ["userId"],
            onDelete = ForeignKey.CASCADE
        )
    ],
    indices = [Index("userId"), Index("loginTime")]
)
data class LoginHistory(
    @PrimaryKey
    val id: String,
    val userId: String,
    val username: String, // Denormalized để query nhanh
    val loginTime: Long,
    val logoutTime: Long? = null,
    val loginStatus: LoginStatus,
    val deviceInfo: String? = null, // Android device info
    val appVersion: String? = null
)

enum class LoginStatus {
    SUCCESS,
    FAILED_INVALID_CREDENTIALS,
    FAILED_ACCOUNT_DISABLED,
    FAILED_UNKNOWN,
    SESSION_TIMEOUT,
    MANUAL_LOGOUT
}