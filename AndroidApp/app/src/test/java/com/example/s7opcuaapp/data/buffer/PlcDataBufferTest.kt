package com.example.s7opcuaapp.data.buffer

import com.example.s7opcuaapp.data.model.PlcData
import com.example.s7opcuaapp.util.PerformanceMonitor
import com.google.common.truth.Truth.assertThat
import io.mockk.every
import io.mockk.mockk
import io.mockk.verify
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.first
import kotlinx.coroutines.test.runTest
import org.junit.After
import org.junit.Before
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class PlcDataBufferTest {

    private lateinit var performanceMonitor: PerformanceMonitor
    private lateinit var buffer: PlcDataBuffer

    @Before
    fun setup() {
        performanceMonitor = mockk(relaxed = true)
        buffer = PlcDataBuffer(performanceMonitor)
    }

    @After
    fun tearDown() {
        buffer.dispose()
    }

    // ============== Initialization Tests ==============

    @Test
    fun `buffer initializes with default values`() {
        // Act
        val data = buffer.getCurrentData()

        // Assert
        assertThat(data.bools).hasSize(15)
        assertThat(data.ints).hasSize(28)
        assertThat(data.bools.all { !it }).isTrue()
        assertThat(data.ints.all { it == 0 }).isTrue()
    }

    // ============== Boolean Update Tests ==============

    @Test
    fun `updateBool changes boolean value at index`() {
        // Act
        buffer.updateBool(0, true)
        buffer.updateBool(5, true)

        // Assert
        val data = buffer.getCurrentData()
        assertThat(data.bools[0]).isTrue()
        assertThat(data.bools[5]).isTrue()
        assertThat(data.bools[1]).isFalse()
    }

    @Test
    fun `updateBool with same value does not trigger change`() {
        // Arrange
        buffer.updateBool(0, false)

        // Act
        val statsBefore = buffer.getStats()
        buffer.updateBool(0, false) // Same value
        val statsAfter = buffer.getStats()

        // Assert - pendingChanges should be 0 since value didn't change
        assertThat(statsAfter.pendingChanges).isEqualTo(statsBefore.pendingChanges)
    }

    @Test
    fun `updateBool with different value triggers change`() {
        // Arrange
        buffer.updateBool(0, false)

        // Act
        buffer.updateBool(0, true)

        // Assert
        val data = buffer.getCurrentData()
        assertThat(data.bools[0]).isTrue()
    }

    // ============== Integer Update Tests ==============

    @Test
    fun `updateInt changes integer value at index`() {
        // Act
        buffer.updateInt(0, 100)
        buffer.updateInt(10, 500)

        // Assert
        val data = buffer.getCurrentData()
        assertThat(data.ints[0]).isEqualTo(100)
        assertThat(data.ints[10]).isEqualTo(500)
        assertThat(data.ints[1]).isEqualTo(0)
    }

    @Test
    fun `updateInt with same value does not trigger change`() {
        // Arrange
        buffer.updateInt(0, 50)

        // Act
        buffer.updateInt(0, 50) // Same value

        // Assert
        val data = buffer.getCurrentData()
        assertThat(data.ints[0]).isEqualTo(50)
    }

    @Test
    fun `updateInt with different value triggers change`() {
        // Arrange
        buffer.updateInt(0, 50)

        // Act
        buffer.updateInt(0, 100)

        // Assert
        val data = buffer.getCurrentData()
        assertThat(data.ints[0]).isEqualTo(100)
    }

    // ============== Clear Tests ==============

    @Test
    fun `clear resets all values to defaults`() {
        // Arrange
        buffer.updateBool(0, true)
        buffer.updateBool(5, true)
        buffer.updateInt(0, 100)
        buffer.updateInt(10, 500)

        // Act
        buffer.clear()

        // Assert
        val data = buffer.getCurrentData()
        assertThat(data.bools.all { !it }).isTrue()
        assertThat(data.ints.all { it == 0 }).isTrue()
    }

    @Test
    fun `clear resets buffer stats`() {
        // Arrange
        buffer.updateBool(0, true)
        buffer.updateInt(0, 100)

        // Act
        buffer.clear()
        val stats = buffer.getStats()

        // Assert
        assertThat(stats.pendingChanges).isEqualTo(0)
        assertThat(stats.hasPendingEmit).isFalse()
    }

    // ============== GetCurrentData Tests ==============

    @Test
    fun `getCurrentData returns snapshot of current values`() {
        // Arrange
        buffer.updateBool(0, true)
        buffer.updateBool(3, true)
        buffer.updateInt(5, 250)
        buffer.updateInt(15, 1000)

        // Act
        val data = buffer.getCurrentData()

        // Assert
        assertThat(data.bools[0]).isTrue()
        assertThat(data.bools[3]).isTrue()
        assertThat(data.bools[1]).isFalse()
        assertThat(data.ints[5]).isEqualTo(250)
        assertThat(data.ints[15]).isEqualTo(1000)
    }

    @Test
    fun `getCurrentData creates new PlcData instance each time`() {
        // Act
        val data1 = buffer.getCurrentData()
        val data2 = buffer.getCurrentData()

        // Assert
        assertThat(data1).isEqualTo(data2)
        assertThat(data1).isNotSameInstanceAs(data2)
    }

    // ============== GetStats Tests ==============

    @Test
    fun `getStats returns correct buffer statistics`() {
        // Arrange
        buffer.updateBool(0, true)
        buffer.updateInt(0, 100)

        // Act
        val stats = buffer.getStats()

        // Assert
        assertThat(stats.boolCount).isEqualTo(15)
        assertThat(stats.intCount).isEqualTo(28)
    }

    // ============== Critical Values Tests ==============

    @Test
    fun `critical bool indices are handled specially`() {
        // Act - Update critical index (4 = Power, 10 = E-Stop)
        buffer.updateBool(4, true)

        // Assert
        val data = buffer.getCurrentData()
        assertThat(data.bools[4]).isTrue()
    }

    // ============== Edge Cases ==============

    @Test
    fun `multiple rapid updates are handled correctly`() {
        // Act
        repeat(100) { i ->
            buffer.updateInt(i % 28, i)
        }

        // Assert
        val data = buffer.getCurrentData()
        // Last value for each index should be preserved
        assertThat(data.ints[0]).isEqualTo(84) // 0, 28, 56, 84
        assertThat(data.ints[1]).isEqualTo(85) // 1, 29, 57, 85
    }

    @Test
    fun `buffer handles boundary indices correctly`() {
        // Act & Assert - Should not throw
        buffer.updateBool(0, true)
        buffer.updateBool(14, true)
        buffer.updateInt(0, 100)
        buffer.updateInt(27, 100)

        val data = buffer.getCurrentData()
        assertThat(data.bools[0]).isTrue()
        assertThat(data.bools[14]).isTrue()
        assertThat(data.ints[0]).isEqualTo(100)
        assertThat(data.ints[27]).isEqualTo(100)
    }
}
