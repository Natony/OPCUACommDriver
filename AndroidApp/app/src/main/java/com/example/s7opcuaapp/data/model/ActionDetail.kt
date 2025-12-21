package com.example.s7opcuaapp.data.model

import com.google.gson.Gson

// Helper classes cho details field trong DeviceAccessLog
sealed class ActionDetail {
    data class WriteBoolDetail(
        val index: Int,
        val oldValue: Boolean,
        val newValue: Boolean,
        val nodeName: String? = null
    ) : ActionDetail()

    data class WriteIntDetail(
        val index: Int,
        val oldValue: Int,
        val newValue: Int,
        val nodeName: String? = null
    ) : ActionDetail()

    data class FunctionExecuteDetail(
        val functionCode: Int,
        val functionName: String,
        val parameters: Map<String, Any>
    ) : ActionDetail()

    data class ConnectionDetail(
        val ipAddress: String,
        val port: Int,
        val duration: Long? = null
    ) : ActionDetail()

    companion object {
        private val gson = Gson()

        fun toJson(detail: ActionDetail): String = gson.toJson(detail)

        fun fromJson(json: String, action: DeviceAction): ActionDetail? {
            return try {
                when (action) {
                    DeviceAction.WRITE_BOOL -> gson.fromJson(json, WriteBoolDetail::class.java)
                    DeviceAction.WRITE_INT -> gson.fromJson(json, WriteIntDetail::class.java)
                    DeviceAction.FUNCTION_EXECUTE -> gson.fromJson(json, FunctionExecuteDetail::class.java)
                    DeviceAction.CONNECT, DeviceAction.DISCONNECT -> gson.fromJson(json, ConnectionDetail::class.java)
                    else -> null
                }
            } catch (e: Exception) {
                null
            }
        }
    }
}