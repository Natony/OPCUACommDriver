package com.example.s7opcuaapp.data.repository

import android.util.Log
import com.example.s7opcuaapp.data.buffer.PlcDataBuffer
import com.example.s7opcuaapp.data.local.AppDatabase
import com.example.s7opcuaapp.data.model.DeviceEntity
import com.example.s7opcuaapp.data.model.PlcData
import com.example.s7opcuaapp.data.opcua.OPCUAClientManager
import com.example.s7opcuaapp.ui.screen.control.ControlUiState
import com.example.s7opcuaapp.util.LoadingTracker
import com.example.s7opcuaapp.util.PerformanceMonitor
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import org.eclipse.milo.opcua.stack.core.UaException
import org.eclipse.milo.opcua.stack.core.types.builtin.DataValue
import org.eclipse.milo.opcua.stack.core.types.builtin.unsigned.UInteger
import java.util.concurrent.atomic.AtomicBoolean
import javax.inject.Inject

/**
 * Optimized OPC UA Repository with buffer and performance improvements
 */
class OptimizedOPCUARepositoryImpl @Inject constructor(
    private var device: DeviceEntity,
    private val database: AppDatabase,
    private val dataBuffer: PlcDataBuffer,
    private val performanceMonitor: PerformanceMonitor
) : S7Repository {

    companion object {
        @Volatile
        private var activeInstance: OptimizedOPCUARepositoryImpl? = null
        private val instanceLock = Any()
    }

    // Use buffer's flow instead of creating our own
    override fun observePlcData(): Flow<PlcData> = dataBuffer.dataFlow

    // Repository scope for coroutines
    private val repositoryScope = CoroutineScope(Dispatchers.IO + SupervisorJob())

    // Connection management
    private val connectionMutex = Mutex()
    private val isStarted = AtomicBoolean(false)
    private val isConnected = AtomicBoolean(false)
    private var connectionJob: Job? = null

    fun isConnected(): Boolean {
        return isConnected.get() && OPCUAClientManager.isConnected()
    }

    // Connection parameters
    private var lastConnectionAttempt = 0L
    private val minConnectionInterval = 2000L

    private var lastUpdateLogTime = 0L
    private var updateCounter = 0

    // Node IDs configuration
    private val boolNodeIds = (3..17).map { "ns=4;i=$it" }
    private val intNodeIds = (18..45).map { "ns=4;i=$it" }

    // Loading tracker
    private val totalNodes = boolNodeIds.size + intNodeIds.size
    private val loadingTracker = LoadingTracker<String>(totalNodes)

    private val _uiState = MutableStateFlow(ControlUiState())

    private val connectionLostScope = CoroutineScope(Dispatchers.Default + SupervisorJob())

    init {
        // Cancel previous instance if exists
        synchronized(instanceLock) {
            // Stop previous instance if exists
            activeInstance?.let {
                Log.d("OPCUARepo", "⚠️ Stopping previous instance")
                runBlocking {
                    it.forceStop()
                }
            }
            activeInstance = this
        }
    }

    // Subscription groups for optimized polling
    private val subscriptionGroups = mapOf(
        "critical" to SubscriptionGroup(
            nodeIds = listOf("ns=4;i=7", "ns=4;i=14"), // Power, E-Stop
            samplingInterval = 250  // Tăng từ 100ms lên 250ms
        ),
        "movement" to SubscriptionGroup(
            nodeIds = listOf("ns=4;i=3", "ns=4;i=4", "ns=4;i=5", "ns=4;i=6"),
            samplingInterval = 500  // Tăng từ 250ms lên 500ms
        ),
        "status" to SubscriptionGroup(
            nodeIds = boolNodeIds.filter { it !in listOf("ns=4;i=7", "ns=4;i=14", "ns=4;i=3", "ns=4;i=4", "ns=4;i=5", "ns=4;i=6") } + intNodeIds,
            samplingInterval = 1000  // Tăng từ 500ms lên 1000ms
        )
    )

    data class SubscriptionGroup(
        val nodeIds: List<String>,
        val samplingInterval: Int
    )

    /**
     * Start connection with improved error handling
     */
    suspend fun start() = connectionMutex.withLock {
        if (isStarted.get()) {
            Log.d("OPCUARepo", "⚠️ Repository already started")
            return@withLock
        }

        Log.d("OPCUARepo", "🚀 Starting Optimized OPC UA Repository...")
        isStarted.set(true)

        // Reset state
        loadingTracker.reset()
        dataBuffer.clear()

        // Make sure no old connection job is running
        connectionJob?.cancel()
        connectionJob = null

        // Wait a bit for cleanup
        delay(500)

        // Start new connection loop
        connectionJob = repositoryScope.launch {
            startConnectionLoop()
        }
    }

    private suspend fun stopInternal() {
        connectionJob?.cancel()
        connectionJob = null

        OPCUAClientManager.disconnect()

        isStarted.set(false)
        isConnected.set(false)
    }

    private suspend fun forceStop() {
        try {
            Log.d("OPCUARepo", "🔴 Force stopping repository")

            // Set flags first
            isStarted.set(false)
            isConnected.set(false)

            // Cancel all jobs
            connectionJob?.cancel()
            connectionJob = null

            connectionLostScope.cancel()

            // Disconnect OPC UA
            try {
                OPCUAClientManager.disconnect()
            } catch (e: Exception) {
                Log.w("OPCUARepo", "Error disconnecting OPC UA", e)
            }

            // Clear buffer
            dataBuffer.clear()

            // Reset loading tracker
            loadingTracker.reset()

            Log.d("OPCUARepo", "✅ Force stop completed")
        } catch (e: Exception) {
            Log.e("OPCUARepo", "Error in force stop", e)
        }
    }

    /**
     * Connection loop with smart retry logic
     */
    private suspend fun startConnectionLoop() {
        var consecutiveFailures = 0
        val maxConsecutiveFailures = 3

        while (isStarted.get() && repositoryScope.isActive) {
            try {
                if (!isConnected.get()) {
                    // Rate limiting
                    val timeSinceLastAttempt = System.currentTimeMillis() - lastConnectionAttempt
                    if (timeSinceLastAttempt < minConnectionInterval) {
                        delay(minConnectionInterval - timeSinceLastAttempt)
                    }

                    Log.d("OPCUARepo", "🔄 Attempting connection (failures: $consecutiveFailures)")
                    lastConnectionAttempt = System.currentTimeMillis()

                    // Cleanup before connection
                    performCleanup(consecutiveFailures)

                    // Connect
                    val connected = connectToServer()

                    if (connected) {
                        consecutiveFailures = 0
                        isConnected.set(true)
                        performanceMonitor.recordNetworkLatency(
                            System.currentTimeMillis() - lastConnectionAttempt
                        )

                        // Subscribe with groups
                        subscribeWithGroups()

                        Log.d("OPCUARepo", "✅ Connected and subscribed successfully")
                    } else {
                        consecutiveFailures++


                        if (consecutiveFailures >= maxConsecutiveFailures) {
                            Log.e("OPCUARepo", "💥 Max failures reached, stopping")
                            // Set loading percent to -1 to indicate error
                            loadingTracker.setError()
                            break
                        }

                        val retryDelay = minOf(5000L * consecutiveFailures, 30000L)
                        Log.w("OPCUARepo", "❌ Connection failed, retry in ${retryDelay/1000}s")
                        delay(retryDelay)
                    }
                } else {
                    // Faster health check - every 2s instead of 15s
                    delay(2000)

                    // Use fast health check
                    val healthy = OPCUAClientManager.checkConnectionHealth()
                    if (!healthy) {
                        Log.w("OPCUARepo", "📡 Connection unhealthy")
                        isConnected.set(false)
                        loadingTracker.setError()

                        // Notify immediately
                        OPCUAClientManager.connectionLostCallback?.invoke()
                    }
                }
            } catch (e: Exception) {
                Log.e("OPCUARepo", "💥 Connection loop error", e)
                isConnected.set(false)
                consecutiveFailures++

                // Set error state if max failures reached
                if (consecutiveFailures >= maxConsecutiveFailures) {
                    loadingTracker.setError()
                }

                delay(5000)
            }
        }
    }

    /**
     * Cleanup with progressive delays
     */
    private suspend fun performCleanup(failureCount: Int) {
        try {
            Log.d("OPCUARepo", "🧹 Performing cleanup...")
            OPCUAClientManager.disconnect()

            val cleanupDelay = when {
                failureCount == 0 -> 1000L
                failureCount <= 2 -> 3000L
                else -> 5000L
            }

            Log.d("OPCUARepo", "⏳ Waiting ${cleanupDelay}ms for cleanup...")
            delay(cleanupDelay)
        } catch (e: Exception) {
            Log.w("OPCUARepo", "Cleanup error", e)
        }
    }

    /**
     * Connect to server
     */
    private suspend fun connectToServer(): Boolean {
        return try {
            // Set callback để nhận thông báo khi mất kết nối
            OPCUAClientManager.setConnectionLostCallback {
                connectionLostScope.launch {
                    Log.e("OPCUARepo", "🔌 Connection lost detected via callback")
                    handleConnectionLost()
                }
            }

            val startTime = System.currentTimeMillis()
            val result = if (!device.opcUsername.isNullOrBlank()) {
                OPCUAClientManager.connect(
                    ipAddress = device.ipAddress,
                    port = device.port,
                    username = device.opcUsername,
                    password = device.opcPassword
                )
            } else {
                OPCUAClientManager.connect(
                    ipAddress = device.ipAddress,
                    port = device.port
                )
            }

            if (result) {
                val connectTime = System.currentTimeMillis() - startTime
                performanceMonitor.recordNetworkLatency(connectTime)
                Log.d("OPCUARepo", "✅ Connected in ${connectTime}ms")
            }

            result
        } catch (e: Exception) {
            Log.e("OPCUARepo", "❌ Connection error: ${e.message}")
            false
        }
    }

    // Add handleConnectionLost method
    private fun handleConnectionLost() {
        Log.w("OPCUARepo", "🔌 Handling connection lost")
        isConnected.set(false)
        loadingTracker.setError()
    }

    private fun getNodeIndex(nodeId: String): Int {
        return try {
            val nodeIndex = nodeId.substringAfter("i=").toInt()
            when {
                nodeIndex in 3..17 -> nodeIndex - 3   // Bool indices 0-14
                nodeIndex in 18..45 -> nodeIndex - 18  // Int indices 0-27
                else -> -1
            }
        } catch (e: Exception) {
            Log.w("OPCUARepo", "Failed to parse node index from $nodeId", e)
            -1
        }
    }

    /**
     * Subscribe with grouped sampling intervals
     */
    private suspend fun subscribeWithGroups() {
        var totalSubscriptions = 0
        val totalNodes = boolNodeIds.size + intNodeIds.size

        subscriptionGroups.forEach { (groupName, group) ->
            Log.d("OPCUARepo", "📊 Subscribing $groupName group (${group.nodeIds.size} nodes, ${group.samplingInterval}ms)")

            for (nodeId in group.nodeIds) {
                try {
                    val index = getNodeIndex(nodeId)
                    if (index < 0) {
                        Log.w("OPCUARepo", "Invalid node index for $nodeId")
                        continue
                    }

                    OPCUAClientManager.createSubscription(
                        nodeIdString = nodeId,
                        samplingInterval = UInteger.valueOf(group.samplingInterval)
                    ) { dataValue ->
                        handleDataUpdate(nodeId, index, dataValue)
                    }

                    totalSubscriptions++
                    loadingTracker.markLoaded(nodeId)

                    // Calculate and log progress
                    val percent = (totalSubscriptions * 100) / totalNodes
                    Log.v("OPCUARepo", "Subscription progress: $percent% ($totalSubscriptions/$totalNodes)")

                    // Small delay to prevent overwhelming
                    delay(20) // Reduced from 50ms for faster loading

                } catch (e: Exception) {
                    Log.e("OPCUARepo", "Failed to subscribe $nodeId", e)
                }
            }
        }

        performanceMonitor.updateSubscriptionCount(totalSubscriptions)
        Log.d("OPCUARepo", "🎯 Created $totalSubscriptions subscriptions")
    }

    /**
     * Handle data updates through buffer
     */
    private fun handleDataUpdate(nodeId: String, index: Int, dataValue: DataValue) {
        if (index < 0) {
            Log.w("OPCUARepo", "Invalid index for node $nodeId")
            return
        }

        try {
            // Log update frequency for debugging
            updateCounter++
            val now = System.currentTimeMillis()
            if (now - lastUpdateLogTime >= 5000) { // Log every 5 seconds
                val rate = updateCounter * 1000.0 / (now - lastUpdateLogTime)
                Log.d("OPCUARepo", "OPC UA update rate: ${String.format("%.1f", rate)}/s")
                updateCounter = 0
                lastUpdateLogTime = now
            }

            val nodeIndex = nodeId.substringAfter("i=").toIntOrNull() ?: return

            when {
                nodeIndex in 3..17 -> {
                    val value = dataValue.value.value as? Boolean ?: false
                    dataBuffer.updateBool(index, value)
                }
                nodeIndex in 18..45 -> {
                    val raw = dataValue.value.value
                    val value = when (raw) {
                        is Int -> raw
                        is Long -> raw.toInt()
                        is Short -> raw.toInt()
                        is UInteger -> raw.toInt()
                        else -> {
                            Log.w("OPCUARepo", "Unknown data type for $nodeId: ${raw?.javaClass}")
                            0
                        }
                    }
                    dataBuffer.updateInt(index, value)
                }
                else -> {
                    Log.w("OPCUARepo", "Unexpected node index: $nodeIndex for $nodeId")
                }
            }
        } catch (e: Exception) {
            Log.w("OPCUARepo", "Error handling update for $nodeId", e)
        }
    }

/**
     * Write boolean with performance tracking
     */
    override suspend fun writeBoolean(index: Int, value: Boolean) = withContext(Dispatchers.IO) {
        if (!isConnected.get()) {
            throw Exception("Not connected to PLC")
        }

        performanceMonitor.recordWriteCommand()

        val startTime = System.currentTimeMillis()
        try {
            if (index in boolNodeIds.indices) {
                val nodeIdString = boolNodeIds[index]
                val status = OPCUAClientManager.writeNode(nodeIdString, value)

                if (status == null || !status.isGood) {
                    throw Exception("Write failed: $status")
                }

                val writeTime = System.currentTimeMillis() - startTime
                performanceMonitor.recordNetworkLatency(writeTime)

                Log.d("OPCUARepo", "✅ WriteBoolean[$index]=$value in ${writeTime}ms")
            }
        } catch (e: Exception) {
            Log.e("OPCUARepo", "❌ WriteBoolean failed", e)

            // Check if connection lost
            if (e.message?.contains("connection", ignoreCase = true) == true ||
                e.message?.contains("timeout", ignoreCase = true) == true ||
                e.message?.contains("Bad_NotConnected", ignoreCase = true) == true) {
                handleConnectionLost()
            }

            throw e
        }
    }

    /**
     * Write integer with performance tracking
     */
    override suspend fun writeInt(index: Int, value: Int) = withContext(Dispatchers.IO) {
        if (!isConnected.get()) {
            throw Exception("Not connected to PLC")
        }

        performanceMonitor.recordWriteCommand()

        val startTime = System.currentTimeMillis()
        try {
            if (index in intNodeIds.indices) {
                val nodeIdString = intNodeIds[index]
                val shortValue = value.toShort()
                val status = OPCUAClientManager.writeNode(nodeIdString, shortValue)

                if (status == null || !status.isGood) {
                    throw Exception("Write failed: $status")
                }

                val writeTime = System.currentTimeMillis() - startTime
                performanceMonitor.recordNetworkLatency(writeTime)

                Log.d("OPCUARepo", "✅ WriteInt[$index]=$value in ${writeTime}ms")
            }
        } catch (e: Exception) {
            Log.e("OPCUARepo", "❌ WriteInt failed", e)

            // Check if connection lost
            if (e.message?.contains("connection", ignoreCase = true) == true ||
                e.message?.contains("timeout", ignoreCase = true) == true ||
                e.message?.contains("Bad_NotConnected", ignoreCase = true) == true) {
                handleConnectionLost()
            }

            throw e
        }
    }

    /**
     * Stop repository
     */
    override fun stop() {
        Log.d("OPCUARepo", "🛑 Stop called")

        runBlocking {
            try {
                // Use force stop for immediate cleanup
                forceStop()

                // Wait for cleanup
                delay(1000)

                // Clear active instance if it's this one
                synchronized(instanceLock) {
                    if (activeInstance == this@OptimizedOPCUARepositoryImpl) {
                        activeInstance = null
                    }
                }

                Log.d("OPCUARepo", "✅ Repository stopped completely")
            } catch (e: Exception) {
                Log.e("OPCUARepo", "Error during stop", e)
            }
        }
    }


    /**
     * Update device configuration
     */
    override fun updateDevice(device: DeviceEntity) {
        Log.d("OPCUARepo", "📱 Updating device: ${device.name}")
        this.device = device

        // Don't auto-restart here, let ViewModel handle it
        if (isStarted.get()) {
            Log.d("OPCUARepo", "Device updated while running - restart needed")
        }
    }

    fun observeLoadingPercent(): StateFlow<Int> = loadingTracker.percent
}