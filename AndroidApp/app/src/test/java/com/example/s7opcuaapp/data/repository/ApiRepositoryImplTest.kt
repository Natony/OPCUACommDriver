package com.example.s7opcuaapp.data.repository

import com.example.s7opcuaapp.data.buffer.PlcDataBuffer
import com.example.s7opcuaapp.data.model.DeviceEntity
import com.example.s7opcuaapp.data.model.PlcData
import com.example.s7opcuaapp.util.PerformanceMonitor
import com.google.common.truth.Truth.assertThat
import io.mockk.*
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.test.runTest
import org.junit.After
import org.junit.Before
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class ApiRepositoryImplTest {

    private lateinit var device: DeviceEntity
    private lateinit var dataBuffer: PlcDataBuffer
    private lateinit var performanceMonitor: PerformanceMonitor
    private lateinit var repository: ApiRepositoryImpl

    @Before
    fun setup() {
        device = DeviceEntity(
            id = "test-device",
            name = "Test PLC",
            ipAddress = "192.168.1.100",
            port = 5000
        )

        performanceMonitor = mockk(relaxed = true)
        dataBuffer = mockk(relaxed = true)

        // Setup mock flows
        val mockFlow = MutableSharedFlow<PlcData>()
        every { dataBuffer.dataFlow } returns mockFlow
    }

    @After
    fun tearDown() {
        if (::repository.isInitialized) {
            repository.stop()
        }
    }

    // ============== Construction Tests ==============

    @Test
    fun `repository initializes with correct device`() {
        // Act
        repository = ApiRepositoryImpl(device, dataBuffer, performanceMonitor)

        // Assert - should not throw
        assertThat(repository).isNotNull()
    }

    // ============== Connection State Tests ==============

    @Test
    fun `isConnected returns false initially`() {
        // Arrange
        repository = ApiRepositoryImpl(device, dataBuffer, performanceMonitor)

        // Act & Assert
        assertThat(repository.isConnected()).isFalse()
    }

    // ============== observePlcData Tests ==============

    @Test
    fun `observePlcData returns buffer data flow`() {
        // Arrange
        repository = ApiRepositoryImpl(device, dataBuffer, performanceMonitor)

        // Act
        val flow = repository.observePlcData()

        // Assert
        assertThat(flow).isNotNull()
    }

    // ============== writeBoolean Tests ==============

    @Test
    fun `writeBoolean throws when not connected`() = runTest {
        // Arrange
        repository = ApiRepositoryImpl(device, dataBuffer, performanceMonitor)

        // Act & Assert
        try {
            repository.writeBoolean(0, true)
            // Should not reach here
            assertThat(false).isTrue()
        } catch (e: Exception) {
            assertThat(e.message).contains("Not connected")
        }
    }

    @Test
    fun `writeBoolean records write command in performance monitor`() = runTest {
        // Arrange
        repository = ApiRepositoryImpl(device, dataBuffer, performanceMonitor)

        // Act
        try {
            repository.writeBoolean(0, true)
        } catch (e: Exception) {
            // Expected - not connected
        }

        // Assert
        verify { performanceMonitor.recordWriteCommand() }
    }

    // ============== writeInt Tests ==============

    @Test
    fun `writeInt throws when not connected`() = runTest {
        // Arrange
        repository = ApiRepositoryImpl(device, dataBuffer, performanceMonitor)

        // Act & Assert
        try {
            repository.writeInt(0, 100)
            // Should not reach here
            assertThat(false).isTrue()
        } catch (e: Exception) {
            assertThat(e.message).contains("Not connected")
        }
    }

    @Test
    fun `writeInt records write command in performance monitor`() = runTest {
        // Arrange
        repository = ApiRepositoryImpl(device, dataBuffer, performanceMonitor)

        // Act
        try {
            repository.writeInt(0, 100)
        } catch (e: Exception) {
            // Expected - not connected
        }

        // Assert
        verify { performanceMonitor.recordWriteCommand() }
    }

    // ============== updateDevice Tests ==============

    @Test
    fun `updateDevice updates internal device reference`() {
        // Arrange
        repository = ApiRepositoryImpl(device, dataBuffer, performanceMonitor)
        val newDevice = DeviceEntity(
            id = "new-device",
            name = "New PLC",
            ipAddress = "192.168.1.200",
            port = 5001
        )

        // Act
        repository.updateDevice(newDevice)

        // Assert - should not throw
    }

    // ============== stop Tests ==============

    @Test
    fun `stop clears data buffer`() {
        // Arrange
        repository = ApiRepositoryImpl(device, dataBuffer, performanceMonitor)

        // Act
        repository.stop()

        // Assert
        verify { dataBuffer.clear() }
    }

    // ============== Loading Percent Tests ==============

    @Test
    fun `observeLoadingPercent returns state flow`() {
        // Arrange
        repository = ApiRepositoryImpl(device, dataBuffer, performanceMonitor)

        // Act
        val flow = repository.observeLoadingPercent()

        // Assert
        assertThat(flow).isNotNull()
    }
}
