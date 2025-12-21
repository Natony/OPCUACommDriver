package com.example.s7opcuaapp.util

import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow

/**
 * Track progress of "first-time" events for a set of keys.
 * Added reset functionality for reconnection scenarios.
 *
 * @param totalKeys Tổng số phần tử cần load.
 */
class LoadingTracker<K>(private val totalKeys: Int) {
    private val seen = mutableSetOf<K>()
    private val _percent = MutableStateFlow(0)

    /** Phần trăm (0..100) đã load xong hoặc -1 nếu error. */
    val percent: StateFlow<Int> = _percent

    /** Đánh dấu key vừa nhận data lần đầu. */
    @Synchronized
    fun markLoaded(key: K) {
        if (seen.add(key)) {
            _percent.value = (seen.size * 100) / totalKeys.coerceAtLeast(1)
        }
    }

    /** True khi đã đạt 100%. */
    val isComplete: Boolean
        get() = seen.size >= totalKeys

    /** Reset tracker để bắt đầu lại quá trình loading (dùng khi reconnect). */
    @Synchronized
    fun reset() {
        seen.clear()
        _percent.value = 0
    }

    /** Set error state */
    fun setError() {
        _percent.value = -1
    }

    /** Get current loading count for debugging */
    val loadedCount: Int
        get() = seen.size
}