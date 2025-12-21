package com.example.s7opcuaapp.util

import android.util.Log
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Job
import kotlinx.coroutines.delay
import kotlinx.coroutines.launch
import javax.inject.Inject
import javax.inject.Singleton

@Singleton
class ConnectionTimeoutManager @Inject constructor() {

    companion object {
        const val CONNECTION_TIMEOUT = 15_000L // 15 seconds per attempt
        const val MAX_RETRY_ATTEMPTS = 3
    }

    private var timeoutJob: Job? = null
    private var retryCount = 0

    // THÊM: Track timeout state
    @Volatile
    private var isTimeoutActive = false

    fun startTimeout(
        scope: CoroutineScope,
        onTimeout: () -> Unit
    ) {
        Log.d("ConnectionTimeoutManager", "Starting timeout (${CONNECTION_TIMEOUT}ms)")

        cancelTimeout()
        isTimeoutActive = true

        timeoutJob = scope.launch {
            try {
                delay(CONNECTION_TIMEOUT)

                if (isTimeoutActive) {
                    Log.w("ConnectionTimeoutManager", "⏰ Connection timeout triggered")
                    onTimeout()
                }
            } catch (e: Exception) {
                Log.d("ConnectionTimeoutManager", "Timeout job cancelled")
            }
        }
    }

    fun cancelTimeout() {
        if (isTimeoutActive) {
            Log.d("ConnectionTimeoutManager", "Cancelling timeout")
        }

        isTimeoutActive = false
        timeoutJob?.cancel()
        timeoutJob = null
    }

    fun shouldRetry(): Boolean {
        val canRetry = retryCount < MAX_RETRY_ATTEMPTS
        Log.d("ConnectionTimeoutManager", "Should retry? $canRetry (attempts: $retryCount/$MAX_RETRY_ATTEMPTS)")
        return canRetry
    }

    fun incrementRetry() {
        retryCount++
        Log.d("ConnectionTimeoutManager", "Retry count incremented to $retryCount")
    }

    fun resetRetry() {
        Log.d("ConnectionTimeoutManager", "Reset retry count (was: $retryCount)")
        retryCount = 0
    }

    fun getCurrentAttempt(): Int = retryCount + 1
    fun getMaxAttempts(): Int = MAX_RETRY_ATTEMPTS
    fun getRemainingAttempts(): Int = MAX_RETRY_ATTEMPTS - retryCount

    // THÊM: Get timeout info for debugging
    fun isActive(): Boolean = isTimeoutActive
    fun getTimeoutDuration(): Long = CONNECTION_TIMEOUT
}