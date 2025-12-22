package com.example.s7opcuaapp.data.api

import com.google.common.truth.Truth.assertThat
import org.junit.Test

class ApiModelsTest {

    // ============== ApiResponse Tests ==============

    @Test
    fun `ApiResponse success with data`() {
        // Arrange
        val data = listOf("item1", "item2")

        // Act
        val response = ApiResponse(
            success = true,
            data = data,
            error = null,
            timestamp = "2024-01-01T00:00:00Z"
        )

        // Assert
        assertThat(response.success).isTrue()
        assertThat(response.data).isEqualTo(data)
        assertThat(response.error).isNull()
    }

    @Test
    fun `ApiResponse failure with error`() {
        // Arrange
        val errorMessage = "Connection failed"

        // Act
        val response = ApiResponse<String>(
            success = false,
            data = null,
            error = errorMessage,
            timestamp = "2024-01-01T00:00:00Z"
        )

        // Assert
        assertThat(response.success).isFalse()
        assertThat(response.data).isNull()
        assertThat(response.error).isEqualTo(errorMessage)
    }

    // ============== PlcDto Tests ==============

    @Test
    fun `PlcDto properties are set correctly`() {
        // Act
        val plc = PlcDto(
            id = "plc-1",
            name = "Main PLC",
            endpointUrl = "opc.tcp://localhost:4840",
            connectionState = "Connected",
            tagCount = 50,
            lastConnected = "2024-01-01T12:00:00Z"
        )

        // Assert
        assertThat(plc.id).isEqualTo("plc-1")
        assertThat(plc.name).isEqualTo("Main PLC")
        assertThat(plc.endpointUrl).isEqualTo("opc.tcp://localhost:4840")
        assertThat(plc.connectionState).isEqualTo("Connected")
        assertThat(plc.tagCount).isEqualTo(50)
        assertThat(plc.lastConnected).isEqualTo("2024-01-01T12:00:00Z")
    }

    @Test
    fun `PlcDto with null optional fields`() {
        // Act
        val plc = PlcDto(
            id = "plc-1",
            name = "Test PLC",
            endpointUrl = "opc.tcp://localhost:4840",
            connectionState = "Disconnected",
            tagCount = 0,
            lastConnected = null
        )

        // Assert
        assertThat(plc.lastConnected).isNull()
    }

    // ============== TagDto Tests ==============

    @Test
    fun `TagDto with integer value`() {
        // Act
        val tag = TagDto(
            id = "tag-1",
            name = "Temperature",
            nodeId = "ns=4;i=100",
            plcId = "plc-1",
            plcName = "Main PLC",
            value = 25,
            quality = "Good",
            timestamp = "2024-01-01T12:00:00Z",
            dataType = "Int32",
            isWritable = true
        )

        // Assert
        assertThat(tag.id).isEqualTo("tag-1")
        assertThat(tag.value).isEqualTo(25)
        assertThat(tag.quality).isEqualTo("Good")
        assertThat(tag.isWritable).isTrue()
    }

    @Test
    fun `TagDto with boolean value`() {
        // Act
        val tag = TagDto(
            id = "tag-2",
            name = "Status",
            nodeId = "ns=4;i=101",
            plcId = "plc-1",
            plcName = "Main PLC",
            value = true,
            quality = "Good",
            timestamp = "2024-01-01T12:00:00Z",
            dataType = "Boolean",
            isWritable = false
        )

        // Assert
        assertThat(tag.value).isEqualTo(true)
        assertThat(tag.dataType).isEqualTo("Boolean")
        assertThat(tag.isWritable).isFalse()
    }

    @Test
    fun `TagDto with null value`() {
        // Act
        val tag = TagDto(
            id = "tag-3",
            name = "Unknown",
            nodeId = "ns=4;i=102",
            plcId = "plc-1",
            plcName = "Main PLC",
            value = null,
            quality = "Bad",
            timestamp = null,
            dataType = "Unknown",
            isWritable = false
        )

        // Assert
        assertThat(tag.value).isNull()
        assertThat(tag.timestamp).isNull()
    }

    // ============== ConnectionStatusDto Tests ==============

    @Test
    fun `ConnectionStatusDto connected state`() {
        // Act
        val status = ConnectionStatusDto(
            plcId = "plc-1",
            plcName = "Main PLC",
            state = "Connected",
            isConnected = true,
            lastStateChange = "2024-01-01T12:00:00Z"
        )

        // Assert
        assertThat(status.isConnected).isTrue()
        assertThat(status.state).isEqualTo("Connected")
    }

    @Test
    fun `ConnectionStatusDto disconnected state`() {
        // Act
        val status = ConnectionStatusDto(
            plcId = "plc-1",
            plcName = "Main PLC",
            state = "Disconnected",
            isConnected = false,
            lastStateChange = "2024-01-01T12:00:00Z"
        )

        // Assert
        assertThat(status.isConnected).isFalse()
        assertThat(status.state).isEqualTo("Disconnected")
    }

    // ============== TagValueUpdate Tests ==============

    @Test
    fun `TagValueUpdate with integer value`() {
        // Act
        val update = TagValueUpdate(
            plcId = "plc-1",
            tagId = "tag-1",
            nodeId = "ns=4;i=100",
            value = 123,
            quality = "Good",
            timestamp = "2024-01-01T12:00:00Z"
        )

        // Assert
        assertThat(update.plcId).isEqualTo("plc-1")
        assertThat(update.tagId).isEqualTo("tag-1")
        assertThat(update.value).isEqualTo(123)
    }

    @Test
    fun `TagValueUpdate with boolean value`() {
        // Act
        val update = TagValueUpdate(
            plcId = "plc-1",
            tagId = "tag-2",
            nodeId = "ns=4;i=101",
            value = false,
            quality = "Good",
            timestamp = "2024-01-01T12:00:00Z"
        )

        // Assert
        assertThat(update.value).isEqualTo(false)
    }

    // ============== WriteTagRequest Tests ==============

    @Test
    fun `WriteTagRequest with integer value`() {
        // Act
        val request = WriteTagRequest(value = 100)

        // Assert
        assertThat(request.value).isEqualTo(100)
    }

    @Test
    fun `WriteTagRequest with boolean value`() {
        // Act
        val request = WriteTagRequest(value = true)

        // Assert
        assertThat(request.value).isEqualTo(true)
    }

    @Test
    fun `WriteTagRequest with string value`() {
        // Act
        val request = WriteTagRequest(value = "test string")

        // Assert
        assertThat(request.value).isEqualTo("test string")
    }
}
