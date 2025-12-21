package com.example.s7opcuaapp.data.model

import androidx.room.Entity
import androidx.room.Index
import androidx.room.PrimaryKey

@Entity(
    tableName = "users",
    indices = [Index(value = ["username"], unique = true)]
)
data class User(
    @PrimaryKey
    val id: String,
    val username: String,
    val passwordHash: String,
    val fullName: String? = null,
    val email: String? = null,
    val role: UserRole = UserRole.VIEWER,
    val isActive: Boolean = true,
    val createdAt: Long = System.currentTimeMillis(),
    val createdBy: String,
    val modifiedAt: Long? = null,
    val modifiedBy: String? = null,
    val lastLoginTime: Long? = null,
    val phoneNumber: String? = null,
    val department: String? = null
) {
    fun hasAdminPrivileges(): Boolean {
        return role == UserRole.ADMIN
    }

    fun canModifyDevices(): Boolean {
        return role == UserRole.ADMIN || role == UserRole.OPERATOR
    }

    fun canViewOnly(): Boolean {
        return role == UserRole.VIEWER
    }
}

enum class UserRole {
    ADMIN,
    OPERATOR,
    VIEWER
}