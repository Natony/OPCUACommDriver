package com.example.s7opcuaapp.data.model

data class Session(
    val sessionId: String,
    val userId: String,
    val user: User,
    val loginTime: Long,
    val lastActivityTime: Long,
    val expiryTime: Long,
    val deviceInfo: String? = null
)