package com.example.s7opcuaapp.data.repository

import android.util.Log
import com.example.s7opcuaapp.data.api.PlcApiClient
import com.example.s7opcuaapp.data.api.TagValueUpdate
import com.example.s7opcuaapp.data.buffer.PlcDataBuffer
import com.example.s7opcuaapp.data.model.DeviceEntity
import com.example.s7opcuaapp.data.model.PlcData
import com.example.s7opcuaapp.util.LoadingTracker
import com.example.s7opcuaapp.util.PerformanceMonitor
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import java.util.concurrent.atomic.AtomicBoolean
import javax.inject.Inject

/**
 * API-based Repository that connects through WPF server instead of direct OPC UA
 * Benefits:
 * - Lighter weight on mobile (no Eclipse Milo)
 * - Server handles OPC UA complexity
 * - Real-time updates via SignalR
 * - Better battery life
 */
class ApiRepositoryImpl @Inject constructor(
    private var device: DeviceEntity,
    private val dataBuffer: PlcDataBuffer,
    private val performanceMonitor: PerformanceMonitor
) : S7Repository {

    companion object {
        private const val TAG = "ApiRepository"

        @Volatile
        private var activeInstance: ApiRepositoryImpl? = null
        private val instanceLock = Any()
    }

    // Use buffer's flow
    override fun observePlcData(): Flow<PlcData> = dataBuffer.dataFlow

    // Repository scope
    private val repositoryScope = CoroutineScope(Dispatchers.IO + SupervisorJob())

    // API Client
    private var apiClient: PlcApiClient? = null

    // Connection management
    private val connectionMutex = Mutex()
    private val isStarted = AtomicBoolean(false)
    private val isConnected = AtomicBoolean(false)
    private var connectionJob: Job? = null
    private var signalRJob: Job? = null

    // Node IDs configuration (same as before)
    private val boolNodeIds = (3..17).map { "ns=4;i=$it" }
    private val intNodeIds = (18..45).map { "ns=4;i=$it" }

    // Loading tracker
    private val totalNodes = boolNodeIds.size + intNodeIds.size
    private val loadingTracker = LoadingTracker<String>(totalNodes)

    // Update tracking
    private var lastUpdateLogTime = 0L
    private var updateCounter = 0

    init {
        synchronized(instanceLock) {
            activeInstance?.let {
                Log.d(TAG, "Stopping previous instance")
                runBlocking { it.forceStop() }
            }
            activeInstance = this
        }
    }

    fun isConnected(): Boolean = isConnected.get()

    fun observeLoadingPercent(): StateFlow<Int> = loadingTracker.percent

    /**
     * Start connection to server
     */
    suspend fun start() = connectionMutex.withLock {
        if (isStarted.get()) {
            Log.d(TAG, "Repository already started")
            return@withLock
        }

        Log.d(TAG, "Starting API Repository...")
        isStarted.set(true)

        // Reset state
        loadingTracker.reset()
        dataBuffer.clear()

        // Cancel previous jobs
        connectionJob?.cancel()
        signalRJob?.cancel()

        delay(500)

        // Start connection
        connectionJob = repositoryScope.launch {
            startConnectionLoop()
        }
    }

    /**
     * Connection loop with retry logic
     */
    private suspend fun startConnectionLoop() {
        var consecutiveFailures = 0
        val maxConsecutiveFailures = 3

        while (isStarted.get() && repositoryScope.isActive) {
            try {
                if (!isConnected.get()) {
                    Log.d(TAG, "Attempting connection (failures: $consecutiveFailures)")

                    // Create API client
                    val serverUrl = "http://${device.ipAddress}:5000"
                    apiClient = PlcApiClient(serverUrl)

                    // Set connection callbacks
                    apiClient?.setConnectionCallbacks(
                        onLost = { handleConnectionLost() },
                        onRestored = { handleConnectionRestored() }
                    )

                    // Connect
                    val connected = apiClient?.connect(device.id) ?: false

                    if (connected) {
                        consecutiveFailures = 0
                        isConnected.set(true)
                        Log.d(TAG, "Connected successfully")

                        // Start listening for SignalR updates
                        startSignalRListener()

                        // Initial data load
                        loadInitialData()

                    } else {
                        consecutiveFailures++
                        if (consecutiveFailures >= maxConsecutiveFailures) {
                            Log.e(TAG, "Max failures reached")
                            loadingTracker.setError()
                            break
                        }

                        val retryDelay = minOf(5000L * consecutiveFailures, 30000L)
                        Log.w(TAG, "Connection failed, retry in ${retryDelay/1000}s")
                        delay(retryDelay)
                    }
                } else {
                    // Health check every 5 seconds
                    delay(5000)

                    val healthy = apiClient?.checkConnectionHealth() ?: false
                    if (!healthy) {
                        Log.w(TAG, "Connection unhealthy")
                        isConnected.set(false)
                        loadingTracker.setError()
                    }
                }
            } catch (e: Exception) {
                Log.e(TAG, "Connection loop error", e)
                isConnected.set(false)
                consecutiveFailures++

                if (consecutiveFailures >= maxConsecutiveFailures) {
                    loadingTracker.setError()
                }

                delay(5000)
            }
        }
    }

    /**
     * Start listening for SignalR updates
     */
    private fun startSignalRListener() {
        signalRJob?.cancel()
        signalRJob = repositoryScope.launch {
            apiClient?.observeTagUpdates()?.collect { update ->
                handleTagUpdate(update)
            }
        }
    }

    /**
     * Load initial data from API
     */
    private suspend fun loadInitialData() {
        try {
            Log.d(TAG, "Loading initial data...")

            val tags = apiClient?.readAllTags() ?: emptyList()
            var loadedCount = 0

            tags.forEach { tag ->
                try {
                    val nodeId = tag.nodeId
                    val nodeIndex = nodeId.substringAfter("i=").toIntOrNull() ?: return@forEach

                    when {
                        nodeIndex in 3..17 -> {
                            val index = nodeIndex - 3
                            val value = parseBoolean(tag.value)
                            dataBuffer.updateBool(index, value)
                            loadingTracker.markLoaded(nodeId)
                            loadedCount++
                        }
                        nodeIndex in 18..45 -> {
                            val index = nodeIndex - 18
                            val value = parseInt(tag.value)
                            dataBuffer.updateInt(index, value)
                            loadingTracker.markLoaded(nodeId)
                            loadedCount++
                        }
                    }
                } catch (e: Exception) {
                    Log.w(TAG, "Error processing tag: ${tag.nodeId}", e)
                }
            }

            Log.d(TAG, "Loaded $loadedCount tags")
        } catch (e: Exception) {
            Log.e(TAG, "Error loading initial data", e)
        }
    }

    /**
     * Handle tag update from SignalR
     */
    private fun handleTagUpdate(update: TagValueUpdate) {
        try {
            updateCounter++
            val now = System.currentTimeMillis()
            if (now - lastUpdateLogTime >= 5000) {
                val rate = updateCounter * 1000.0 / (now - lastUpdateLogTime)
                Log.d(TAG, "Update rate: ${String.format("%.1f", rate)}/s")
                updateCounter = 0
                lastUpdateLogTime = now
            }

            val nodeId = update.nodeId
            val nodeIndex = nodeId.substringAfter("i=").toIntOrNull() ?: return

            when {
                nodeIndex in 3..17 -> {
                    val index = nodeIndex - 3
                    val value = parseBoolean(update.value)
                    dataBuffer.updateBool(index, value)
                }
                nodeIndex in 18..45 -> {
                    val index = nodeIndex - 18
                    val value = parseInt(update.value)
                    dataBuffer.updateInt(index, value)
                }
            }
        } catch (e: Exception) {
            Log.w(TAG, "Error handling update", e)
        }
    }

    private fun parseBoolean(value: Any?): Boolean {
        return when (value) {
            is Boolean -> value
            is Number -> value.toInt() != 0
            is String -> value.equals("true", ignoreCase = true)
            else -> false
        }
    }

    private fun parseInt(value: Any?): Int {
        return when (value) {
            is Number -> value.toInt()
            is String -> value.toIntOrNull() ?: 0
            else -> 0
        }
    }

    private fun handleConnectionLost() {
        Log.w(TAG, "Connection lost")
        isConnected.set(false)
        loadingTracker.setError()
    }

    private fun handleConnectionRestored() {
        Log.d(TAG, "Connection restored")
        isConnected.set(true)
    }

    /**
     * Write boolean value
     */
    override suspend fun writeBoolean(index: Int, value: Boolean) = withContext(Dispatchers.IO) {
        if (!isConnected.get()) {
            throw Exception("Not connected to server")
        }

        performanceMonitor.recordWriteCommand()

        val startTime = System.currentTimeMillis()
        try {
            if (index in boolNodeIds.indices) {
                val nodeId = boolNodeIds[index]
                val success = apiClient?.writeBoolean(nodeId, value) ?: false

                if (!success) {
                    throw Exception("Write failed")
                }

                val writeTime = System.currentTimeMillis() - startTime
                performanceMonitor.recordNetworkLatency(writeTime)

                Log.d(TAG, "WriteBoolean[$index]=$value in ${writeTime}ms")
            }
        } catch (e: Exception) {
            Log.e(TAG, "WriteBoolean failed", e)
            throw e
        }
    }

    /**
     * Write integer value
     */
    override suspend fun writeInt(index: Int, value: Int) = withContext(Dispatchers.IO) {
        if (!isConnected.get()) {
            throw Exception("Not connected to server")
        }

        performanceMonitor.recordWriteCommand()

        val startTime = System.currentTimeMillis()
        try {
            if (index in intNodeIds.indices) {
                val nodeId = intNodeIds[index]
                val success = apiClient?.writeInt(nodeId, value) ?: false

                if (!success) {
                    throw Exception("Write failed")
                }

                val writeTime = System.currentTimeMillis() - startTime
                performanceMonitor.recordNetworkLatency(writeTime)

                Log.d(TAG, "WriteInt[$index]=$value in ${writeTime}ms")
            }
        } catch (e: Exception) {
            Log.e(TAG, "WriteInt failed", e)
            throw e
        }
    }

    /**
     * Stop repository
     */
    override fun stop() {
        Log.d(TAG, "Stop called")

        runBlocking {
            try {
                forceStop()
                delay(500)

                synchronized(instanceLock) {
                    if (activeInstance == this@ApiRepositoryImpl) {
                        activeInstance = null
                    }
                }

                Log.d(TAG, "Repository stopped")
            } catch (e: Exception) {
                Log.e(TAG, "Error during stop", e)
            }
        }
    }

    private suspend fun forceStop() {
        try {
            Log.d(TAG, "Force stopping repository")

            isStarted.set(false)
            isConnected.set(false)

            connectionJob?.cancel()
            signalRJob?.cancel()
            connectionJob = null
            signalRJob = null

            apiClient?.disconnect()
            apiClient?.cleanup()
            apiClient = null

            dataBuffer.clear()
            loadingTracker.reset()

            Log.d(TAG, "Force stop completed")
        } catch (e: Exception) {
            Log.e(TAG, "Error in force stop", e)
        }
    }

    /**
     * Update device configuration
     */
    override fun updateDevice(device: DeviceEntity) {
        Log.d(TAG, "Updating device: ${device.name}")
        this.device = device

        if (isStarted.get()) {
            Log.d(TAG, "Device updated while running - restart needed")
        }
    }
}
