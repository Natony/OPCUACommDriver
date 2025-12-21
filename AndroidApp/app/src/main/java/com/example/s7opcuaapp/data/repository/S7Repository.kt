package com.example.s7opcuaapp.data.repository

import com.example.s7opcuaapp.data.model.DeviceEntity
import com.example.s7opcuaapp.data.model.PlcData
import kotlinx.coroutines.flow.Flow

/**
 * Giao diện chung cho repository:
 * - Nếu PLC hỗ trợ OPC UA thì implement qua OPCUARepositoryImpl
 */
interface S7Repository {
    /** Flow phát ra PlcData mỗi khi có thay đổi từ PLC */
    fun observePlcData(): Flow<PlcData>

    /** Ghi một Boolean (bit) xuống PLC */
    suspend fun writeBoolean(index: Int, value: Boolean)

    /** Ghi một Int (DInt) xuống PLC */
    suspend fun writeInt(index: Int, value: Int)

    /** Dừng mọi kết nối/subscription */
    fun stop()

    /** Cập nhật thông tin Device (IP, port, credentials…) */
    fun updateDevice(device: DeviceEntity)
}
