package com.example.s7opcuaapp.data.model

import com.google.common.truth.Truth.assertThat
import org.junit.Test

class PlcDataTest {

    @Test
    fun `empty creates PlcData with default values`() {
        // Act
        val data = PlcData.empty()

        // Assert
        assertThat(data.bools).hasSize(14)
        assertThat(data.ints).hasSize(27)
        assertThat(data.bools.all { !it }).isTrue()
        assertThat(data.ints.all { it == 0 }).isTrue()
    }

    @Test
    fun `PlcData can store boolean values`() {
        // Arrange
        val boolList = listOf(true, false, true, false, true)
        val intList = listOf(1, 2, 3)

        // Act
        val data = PlcData(bools = boolList, ints = intList)

        // Assert
        assertThat(data.bools).isEqualTo(boolList)
        assertThat(data.bools[0]).isTrue()
        assertThat(data.bools[1]).isFalse()
        assertThat(data.bools[2]).isTrue()
    }

    @Test
    fun `PlcData can store integer values`() {
        // Arrange
        val boolList = listOf(false, false)
        val intList = listOf(100, 200, 300, 400, 500)

        // Act
        val data = PlcData(bools = boolList, ints = intList)

        // Assert
        assertThat(data.ints).isEqualTo(intList)
        assertThat(data.ints[0]).isEqualTo(100)
        assertThat(data.ints[4]).isEqualTo(500)
    }

    @Test
    fun `PlcData equality check works correctly`() {
        // Arrange
        val data1 = PlcData(
            bools = listOf(true, false, true),
            ints = listOf(1, 2, 3)
        )
        val data2 = PlcData(
            bools = listOf(true, false, true),
            ints = listOf(1, 2, 3)
        )
        val data3 = PlcData(
            bools = listOf(false, false, true),
            ints = listOf(1, 2, 3)
        )

        // Assert
        assertThat(data1).isEqualTo(data2)
        assertThat(data1).isNotEqualTo(data3)
    }

    @Test
    fun `PlcData copy creates new instance with modified values`() {
        // Arrange
        val original = PlcData(
            bools = listOf(true, false),
            ints = listOf(100, 200)
        )

        // Act
        val modified = original.copy(
            bools = listOf(false, true)
        )

        // Assert
        assertThat(modified.bools).isEqualTo(listOf(false, true))
        assertThat(modified.ints).isEqualTo(listOf(100, 200))
        assertThat(original.bools).isEqualTo(listOf(true, false))
    }

    @Test
    fun `PlcData with empty lists`() {
        // Act
        val data = PlcData(bools = emptyList(), ints = emptyList())

        // Assert
        assertThat(data.bools).isEmpty()
        assertThat(data.ints).isEmpty()
    }

    @Test
    fun `PlcData with large datasets`() {
        // Arrange
        val largeBoolList = (0 until 100).map { it % 2 == 0 }
        val largeIntList = (0 until 100).map { it * 10 }

        // Act
        val data = PlcData(bools = largeBoolList, ints = largeIntList)

        // Assert
        assertThat(data.bools).hasSize(100)
        assertThat(data.ints).hasSize(100)
        assertThat(data.bools[0]).isTrue()
        assertThat(data.bools[1]).isFalse()
        assertThat(data.ints[0]).isEqualTo(0)
        assertThat(data.ints[99]).isEqualTo(990)
    }

    @Test
    fun `PlcData hashCode is consistent`() {
        // Arrange
        val data1 = PlcData(
            bools = listOf(true, false),
            ints = listOf(1, 2)
        )
        val data2 = PlcData(
            bools = listOf(true, false),
            ints = listOf(1, 2)
        )

        // Assert
        assertThat(data1.hashCode()).isEqualTo(data2.hashCode())
    }

    @Test
    fun `PlcData toString includes data`() {
        // Arrange
        val data = PlcData(
            bools = listOf(true, false),
            ints = listOf(100, 200)
        )

        // Act
        val str = data.toString()

        // Assert
        assertThat(str).contains("true")
        assertThat(str).contains("false")
        assertThat(str).contains("100")
        assertThat(str).contains("200")
    }
}
