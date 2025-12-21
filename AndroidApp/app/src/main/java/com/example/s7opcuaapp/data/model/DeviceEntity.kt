package com.example.s7opcuaapp.data.model

import androidx.room.Entity
import androidx.room.PrimaryKey

/**
 * Model lưu thông tin thiết bị PLC với Room Entity support
 */
@Entity(tableName = "devices")
data class DeviceEntity(
    @PrimaryKey
    val id: String,
    val name: String,
    val ipAddress: String,
    val port: Int = 4840,
    val opcUsername: String = "",
    val opcPassword: String = "",
    val useOpcUa: Boolean = true
)