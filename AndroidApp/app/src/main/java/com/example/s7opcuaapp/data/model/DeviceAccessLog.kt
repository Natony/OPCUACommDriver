package com.example.s7opcuaapp.data.model

import androidx.room.Entity
import androidx.room.Index
import androidx.room.PrimaryKey

@Entity(
    tableName = "device_access_logs",
    indices = [
        Index(value = ["userId"]),
        Index(value = ["deviceId"]),
        Index(value = ["timestamp"]),
        Index(value = ["action"])
    ]
)
data class DeviceAccessLog(
    @PrimaryKey(autoGenerate = true)
    val id: Long = 0,
    val userId: String,
    val username: String,
    val deviceId: String,
    val deviceName: String,
    val action: String, // "READ", "WRITE", "CONNECT", "DISCONNECT"
    val timestamp: Long = System.currentTimeMillis(),
    val details: String? = null,
    val ipAddress: String? = null,
    val success: Boolean = true,
    val errorMessage: String? = null
) {
    companion object {
        // Các action types
        const val ACTION_CONNECT = "CONNECT"
        const val ACTION_DISCONNECT = "DISCONNECT"
        const val ACTION_READ = "READ"
        const val ACTION_WRITE = "WRITE"
        const val ACTION_CONFIG_CHANGE = "CONFIG_CHANGE"
        const val ACTION_LOGIN = "LOGIN"
        const val ACTION_LOGOUT = "LOGOUT"
    }
}