package com.example.s7opcuaapp.util

import com.example.s7opcuaapp.data.model.UserRole

object PermissionUtils {

    enum class Permission {
        // Control permissions
        VIEW_CONTROL,
        WRITE_PLC,
        EMERGENCY_STOP,

        // Config permissions
        VIEW_CONFIG,
        ADD_DEVICE,
        EDIT_DEVICE,
        DELETE_DEVICE,

        // User management permissions
        VIEW_USERS,
        ADD_USER,
        EDIT_USER,
        DELETE_USER,

        // Log permissions
        VIEW_OWN_LOGS,
        VIEW_ALL_LOGS,
        EXPORT_LOGS,
        DELETE_LOGS,

        // System permissions
        SYSTEM_CONFIG,
        BACKUP_RESTORE
    }

    private val rolePermissions = mapOf(
        UserRole.ADMIN to setOf(
            Permission.VIEW_CONTROL,
            Permission.WRITE_PLC,
            Permission.EMERGENCY_STOP,
            Permission.VIEW_CONFIG,
            Permission.ADD_DEVICE,
            Permission.EDIT_DEVICE,
            Permission.DELETE_DEVICE,
            Permission.VIEW_USERS,
            Permission.ADD_USER,
            Permission.EDIT_USER,
            Permission.DELETE_USER,
            Permission.VIEW_ALL_LOGS,
            Permission.EXPORT_LOGS,
            Permission.DELETE_LOGS,
            Permission.SYSTEM_CONFIG,
            Permission.BACKUP_RESTORE
        ),
        UserRole.OPERATOR to setOf(
            Permission.VIEW_CONTROL,
            Permission.WRITE_PLC,
            Permission.EMERGENCY_STOP,
            Permission.VIEW_CONFIG,
            Permission.VIEW_OWN_LOGS
        ),
        UserRole.VIEWER to setOf(
            Permission.VIEW_CONTROL,
            Permission.VIEW_CONFIG
        )
    )

    fun hasPermission(role: UserRole, permission: Permission): Boolean {
        return rolePermissions[role]?.contains(permission) ?: false
    }

    fun getPermissions(role: UserRole): Set<Permission> {
        return rolePermissions[role] ?: emptySet()
    }
}