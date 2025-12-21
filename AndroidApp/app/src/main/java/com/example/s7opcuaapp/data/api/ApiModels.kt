package com.example.s7opcuaapp.data.api

import com.google.gson.annotations.SerializedName

/**
 * API Response wrapper
 */
data class ApiResponse<T>(
    @SerializedName("Success") val success: Boolean,
    @SerializedName("Data") val data: T?,
    @SerializedName("Error") val error: String?,
    @SerializedName("Timestamp") val timestamp: String?
)

/**
 * PLC information from API
 */
data class PlcDto(
    @SerializedName("Id") val id: String,
    @SerializedName("Name") val name: String,
    @SerializedName("EndpointUrl") val endpointUrl: String,
    @SerializedName("ConnectionState") val connectionState: String,
    @SerializedName("TagCount") val tagCount: Int,
    @SerializedName("LastConnected") val lastConnected: String?
)

/**
 * Tag information from API
 */
data class TagDto(
    @SerializedName("Id") val id: String?,
    @SerializedName("Name") val name: String?,
    @SerializedName("NodeId") val nodeId: String,
    @SerializedName("PlcId") val plcId: String?,
    @SerializedName("PlcName") val plcName: String?,
    @SerializedName("Value") val value: Any?,
    @SerializedName("Quality") val quality: String?,
    @SerializedName("Timestamp") val timestamp: String?,
    @SerializedName("DataType") val dataType: String?,
    @SerializedName("IsWritable") val isWritable: Boolean?
)

/**
 * Connection status from API
 */
data class ConnectionStatusDto(
    @SerializedName("PlcId") val plcId: String,
    @SerializedName("PlcName") val plcName: String,
    @SerializedName("State") val state: String,
    @SerializedName("IsConnected") val isConnected: Boolean,
    @SerializedName("LastStateChange") val lastStateChange: String?
)

/**
 * Write tag request
 */
data class WriteTagRequest(
    @SerializedName("Value") val value: Any
)

/**
 * Tag value update from SignalR
 */
data class TagValueUpdate(
    @SerializedName("PlcId") val plcId: String,
    @SerializedName("TagId") val tagId: String,
    @SerializedName("NodeId") val nodeId: String,
    @SerializedName("Value") val value: Any?,
    @SerializedName("Quality") val quality: String,
    @SerializedName("Timestamp") val timestamp: String
)
