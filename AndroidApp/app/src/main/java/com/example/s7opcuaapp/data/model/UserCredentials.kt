package com.example.s7opcuaapp.data.model

/**
 * Lưu thông tin Login (username + password) nếu cần.
 */
data class UserCredentials(
    val username: String,
    val password: String
)