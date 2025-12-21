package com.example.s7opcuaapp.data.model

/**
 * Model chứa dữ liệu PLC (mã booleans và ints).
 */
data class PlcData(
    val bools: List<Boolean>,
    val ints: List<Int>
) {
    companion object {
        fun empty(): PlcData {
            return PlcData(
                bools = List(14) { false },
                ints = List(27) { 0 }
            )
        }
    }
}
