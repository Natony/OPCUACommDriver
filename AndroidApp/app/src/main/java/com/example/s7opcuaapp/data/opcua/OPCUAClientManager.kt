package com.example.s7opcuaapp.data.opcua

import android.util.Log
import kotlinx.coroutines.*
import org.eclipse.milo.opcua.sdk.client.OpcUaClient
import org.eclipse.milo.opcua.sdk.client.SessionActivityListener
import org.eclipse.milo.opcua.sdk.client.api.UaSession
import org.eclipse.milo.opcua.sdk.client.api.config.OpcUaClientConfig
import org.eclipse.milo.opcua.sdk.client.api.identity.AnonymousProvider
import org.eclipse.milo.opcua.sdk.client.api.identity.UsernameProvider
import org.eclipse.milo.opcua.sdk.client.api.subscriptions.UaSubscription
import org.eclipse.milo.opcua.sdk.client.api.subscriptions.UaSubscriptionManager.SubscriptionListener
import org.eclipse.milo.opcua.stack.client.DiscoveryClient
import org.eclipse.milo.opcua.stack.core.Identifiers
import org.eclipse.milo.opcua.stack.core.types.builtin.LocalizedText
import org.eclipse.milo.opcua.stack.core.types.builtin.unsigned.UInteger
import org.eclipse.milo.opcua.stack.core.AttributeId
import org.eclipse.milo.opcua.stack.core.types.enumerated.UserTokenType
import org.eclipse.milo.opcua.stack.core.types.structured.EndpointDescription
import org.eclipse.milo.opcua.stack.core.types.structured.UserTokenPolicy
import org.eclipse.milo.opcua.stack.core.security.SecurityPolicy
import org.eclipse.milo.opcua.stack.core.UaException
import org.eclipse.milo.opcua.stack.core.StatusCodes
import java.util.concurrent.CompletableFuture
import java.util.concurrent.TimeUnit
import org.eclipse.milo.opcua.stack.core.types.builtin.DataValue
import org.eclipse.milo.opcua.stack.core.types.builtin.Variant
import org.eclipse.milo.opcua.stack.core.types.builtin.NodeId
import org.eclipse.milo.opcua.stack.core.types.builtin.StatusCode
import org.eclipse.milo.opcua.stack.core.types.builtin.unsigned.UByte
import org.eclipse.milo.opcua.stack.core.types.builtin.unsigned.Unsigned
import org.eclipse.milo.opcua.stack.core.types.structured.ReadValueId
import org.eclipse.milo.opcua.stack.core.types.structured.MonitoringParameters
import org.eclipse.milo.opcua.stack.core.types.structured.MonitoredItemCreateRequest
import org.eclipse.milo.opcua.stack.core.types.enumerated.MonitoringMode
import org.eclipse.milo.opcua.stack.core.types.enumerated.TimestampsToReturn
import org.eclipse.milo.opcua.stack.core.types.builtin.DateTime
import java.util.concurrent.atomic.AtomicBoolean
import java.util.concurrent.ConcurrentHashMap

object OPCUAClientManager {
    var client: OpcUaClient? = null
        private set

    private var subscription: UaSubscription? = null

    // Connection state tracking
    private val isConnectionLost = AtomicBoolean(false)

    // Track last successful data receive time for each node
    private val lastDataReceived = ConcurrentHashMap<String, Long>()
    private var connectionMonitorJob: Job? = null
    private val monitorScope = CoroutineScope(Dispatchers.IO + SupervisorJob())

    // Timeout constants
    private const val DISCOVERY_TIMEOUT = 10000L  // Increased from 5000
    private const val CONNECTION_TIMEOUT = 20000L // Increased from 10000
    private const val OPERATION_TIMEOUT = 10000L  // Increased from 5000
    private const val DATA_TIMEOUT = 15000L       // Increased from 10000

    private var sessionListener: SessionActivityListener? = null
    private var subscriptionListener: SubscriptionListener? = null

    internal var connectionLostCallback: (() -> Unit)? = null
    private var connectionRestoredCallback: (() -> Unit)? = null

    // Fast detection flag
    @Volatile
    private var lastSessionActiveTime = 0L
    private const val SESSION_TIMEOUT = 10000L


    fun setConnectionCallbacks(
        onLost: (() -> Unit)? = null,
        onRestored: (() -> Unit)? = null
    ) {
        connectionLostCallback = onLost
        connectionRestoredCallback = onRestored
    }

    /**
     * Create SessionActivityListener
     */
    private fun createSessionListener(): SessionActivityListener {
        return object : SessionActivityListener {
            override fun onSessionActive(session: UaSession) {
                Log.d("OPCUAClient", "✅ Session ACTIVE")
                lastSessionActiveTime = System.currentTimeMillis()

                // Reset connection lost flag
                if (isConnectionLost.getAndSet(false)) {
                    Log.d("OPCUAClient", "🔄 Connection restored")
                    connectionRestoredCallback?.invoke()
                }
            }

            override fun onSessionInactive(session: UaSession) {
                Log.w("OPCUAClient", "⚠️ Session INACTIVE")
                handleConnectionLost("Session inactive")
            }
        }
    }

    suspend fun checkConnectionHealth(): Boolean = withContext(Dispatchers.IO) {
        try {
            val cli = client
            if (cli == null || isConnectionLost.get()) {
                return@withContext false
            }

            // More lenient time check
            val timeSinceActive = System.currentTimeMillis() - lastSessionActiveTime

            // Only flag as unhealthy if really long time
            if (lastSessionActiveTime > 0 && timeSinceActive > SESSION_TIMEOUT * 2) {
                Log.w("OPCUAClient", "Session timeout: ${timeSinceActive}ms > ${SESSION_TIMEOUT * 2}ms")
                return@withContext false
            }

            // If recently active, assume healthy
            if (timeSinceActive < SESSION_TIMEOUT / 2) {
                return@withContext true
            }

            // Otherwise do actual health check with longer timeout
            withTimeoutOrNull(3000L) { // Increased from 1500ms
                val future = cli.readValue(
                    0.0,
                    TimestampsToReturn.Neither,
                    Identifiers.Server_ServerStatus_State
                )
                future.get(3000, TimeUnit.MILLISECONDS)
                lastSessionActiveTime = System.currentTimeMillis()
                true
            } ?: false

        } catch (e: Exception) {
            Log.e("OPCUAClient", "Health check failed", e)
            false
        }
    }

    /**
     * Create SubscriptionListener
     */
    private fun createSubscriptionListener(): SubscriptionListener {
        return object : SubscriptionListener {
            override fun onSubscriptionTransferFailed(
                subscription: UaSubscription,
                statusCode: StatusCode
            ) {
                Log.e("OPCUAClient", "❌ Subscription transfer failed: $statusCode")
                handleConnectionLost("Subscription transfer failed")
            }

            override fun onStatusChanged(
                subscription: UaSubscription,
                status: StatusCode
            ) {
                Log.d("OPCUAClient", "Subscription status changed: $status")

                if (!status.isGood) {
                    Log.w("OPCUAClient", "⚠️ Subscription status not good: $status")
                }
            }

            override fun onSubscriptionWatchdogTimerElapsed(subscription: UaSubscription) {
                Log.w("OPCUAClient", "⚠️ Subscription watchdog timer elapsed")
                handleConnectionLost("Subscription watchdog timeout")
            }

            override fun onPublishFailure(exception: UaException) {
                Log.e("OPCUAClient", "❌ Publish failure", exception)

                // Check if it's a connection-related failure
                if (exception.statusCode.value == StatusCodes.Bad_ConnectionClosed ||
                    exception.statusCode.value == StatusCodes.Bad_NotConnected ||
                    exception.statusCode.value == StatusCodes.Bad_SessionClosed) {
                    handleConnectionLost("Publish failure: ${exception.message}")
                }
            }

            override fun onNotificationDataLost(subscription: UaSubscription) {
                Log.w("OPCUAClient", "⚠️ Notification data lost")
            }
        }
    }

    /**
     * Set callback for connection lost events
     */
    fun setConnectionLostCallback(callback: (() -> Unit)) {
        connectionLostCallback = callback
    }

    /**
     * Handle connection lost
     */
    private fun handleConnectionLost(reason: String) {
        if (!isConnectionLost.getAndSet(true)) {
            Log.e("OPCUAClient", "🔌 CONNECTION LOST: $reason")
            connectionLostCallback?.invoke()
        }
    }

    /**
     * Start monitoring connection health
     */
    private fun startConnectionMonitor() {
        connectionMonitorJob?.cancel()
        connectionMonitorJob = monitorScope.launch {
            while (isActive) {
                delay(5000) // Check every 5 seconds instead of 2

                try {
                    val now = System.currentTimeMillis()

                    // Only check if we think we're connected
                    if (client != null && !isConnectionLost.get()) {
                        // More lenient session check
                        if (lastSessionActiveTime > 0) {
                            val timeSinceActive = now - lastSessionActiveTime

                            // Only worry if really long time without activity
                            if (timeSinceActive > SESSION_TIMEOUT * 2) {
                                Log.w("OPCUAClient", "⚠️ No session activity for ${timeSinceActive}ms")

                                // Try health check before declaring lost
                                val isHealthy = performHealthCheck()
                                if (!isHealthy) {
                                    handleConnectionLost("No activity and health check failed")
                                    break
                                }
                            }
                        }

                        // Check data flow with more lenient timeout
                        val hasRecentData = lastDataReceived.values.any {
                            (now - it) < DATA_TIMEOUT * 2  // More lenient
                        }

                        // Only check if we expect data
                        if (!hasRecentData && lastDataReceived.isNotEmpty()) {
                            Log.w("OPCUAClient", "⚠️ No data for long time")

                            // Try health check
                            val isHealthy = performHealthCheck()
                            if (!isHealthy) {
                                handleConnectionLost("No data and health check failed")
                                break
                            }
                        }
                    }
                } catch (e: Exception) {
                    Log.e("OPCUAClient", "Monitor error", e)
                }
            }
        }
    }

    /**
     * Perform health check by reading server state
     */
    private suspend fun performHealthCheck(): Boolean = withContext(Dispatchers.IO) {
        try {
            client?.let { cli ->
                // Try to read server state
                val future = cli.readValue(
                    0.0,
                    TimestampsToReturn.Neither,
                    Identifiers.Server_ServerStatus_State
                )

                val result = try {
                    withTimeoutOrNull(1500L) {
                        future.get(1500, TimeUnit.MILLISECONDS)
                        true
                    }
                } catch (e: Exception) {
                    Log.e("OPCUAClient", "Health check read failed: ${e.message}")
                    false
                } ?: false

                if (result) {
                    // Update last active time on successful health check
                    lastSessionActiveTime = System.currentTimeMillis()
                }

                result
            } ?: false
        } catch (e: Exception) {
            Log.e("OPCUAClient", "Health check exception", e)
            false
        }
    }
    /**
     * Connect với better error handling
     */
    suspend fun connect(
        ipAddress: String,
        port: Int,
        username: String? = null,
        password: String? = null
    ): Boolean = withContext(Dispatchers.IO) {
        return@withContext try {
            isConnectionLost.set(false)
            lastDataReceived.clear()

            val endpointUrl = "opc.tcp://$ipAddress:$port"
            Log.d("OPCUAClient", "🔌 Connecting to $endpointUrl...")

            // Discovery với timeout
            val endpoints: List<EndpointDescription> = try {
                withTimeout(DISCOVERY_TIMEOUT) {
                    val future = DiscoveryClient.getEndpoints(endpointUrl)
                    future.get(DISCOVERY_TIMEOUT, TimeUnit.MILLISECONDS)
                }
            } catch (e: Exception) {
                Log.e("OPCUAClient", "❌ Discovery error", e)
                throw e
            }

            val noSecEndpoint = endpoints.firstOrNull {
                it.securityPolicyUri == SecurityPolicy.None.uri
            } ?: throw Exception("No SecurityPolicy.None endpoint found")

            val tokenPoliciesList = noSecEndpoint.userIdentityTokens?.toList() ?: emptyList()

            val identityProvider = when {
                !username.isNullOrBlank() &&
                        tokenPoliciesList.any { it.tokenType == UserTokenType.UserName } -> {
                    UsernameProvider(username, password ?: "")
                }
                else -> AnonymousProvider()
            }

            val config = OpcUaClientConfig.builder()
                .setApplicationName(LocalizedText.english("AndroidUAClient"))
                .setApplicationUri("urn:android:opcua:client")
                .setEndpoint(noSecEndpoint)
                .setIdentityProvider(identityProvider)
                .setRequestTimeout(UInteger.valueOf(5000))
                .setKeepAliveInterval(UInteger.valueOf(5000))
                .build()

            client = OpcUaClient.create(config)

            // Connect với timeout
            try {
                withTimeout(CONNECTION_TIMEOUT) {
                    val connectFuture = client!!.connect()
                    connectFuture.get(CONNECTION_TIMEOUT, TimeUnit.MILLISECONDS)
                }
            } catch (e: Exception) {
                Log.e("OPCUAClient", "❌ Connect error", e)
                client = null
                throw e
            }

            // Add listeners after successful connection
            client?.let { cli ->
                // Add session listener
                sessionListener = createSessionListener()
                cli.addSessionActivityListener(sessionListener!!)

                // Add subscription listener
                subscriptionListener = createSubscriptionListener()
                cli.subscriptionManager.addSubscriptionListener(subscriptionListener!!)

                Log.d("OPCUAClient", "✅ All listeners registered")
            }

            // Start connection monitor
            startConnectionMonitor()

            Log.d("OPCUAClient", "✅ Connected successfully")
            lastSessionActiveTime = System.currentTimeMillis()
            true

        } catch (e: Exception) {
            Log.e("OPCUAClient", "❌ Connection failed", e)
            handleConnectionLost("Connect failed: ${e.message}")
            false
        }
    }

    /**
     * Create subscription với data tracking
     */
    suspend fun createSubscription(
        nodeIdString: String,
        samplingInterval: UInteger = UInteger.valueOf(250),
        onValueChange: (DataValue) -> Unit
    ) = withContext(Dispatchers.IO) {
        val cli = client ?: return@withContext

        try {
            withTimeout(OPERATION_TIMEOUT) {
                if (subscription == null) {
                    val subFuture = cli.subscriptionManager.createSubscription(250.0)
                    subscription = subFuture.get(OPERATION_TIMEOUT, TimeUnit.MILLISECONDS)
                    Log.d("OPCUAClient", "📡 Subscription created")
                }

                val sub = subscription!!

                val readValueId = ReadValueId(
                    NodeId.parse(nodeIdString),
                    AttributeId.Value.uid(),
                    null,
                    null
                )

                val clientHandle = UInteger.valueOf((System.nanoTime() % Int.MAX_VALUE).toInt())
                val monitoringParams = MonitoringParameters(
                    clientHandle,
                    samplingInterval.toDouble(),
                    null,
                    UInteger.valueOf(1),
                    true
                )

                val request = MonitoredItemCreateRequest(
                    readValueId,
                    MonitoringMode.Reporting,
                    monitoringParams
                )

                val itemsFuture = sub.createMonitoredItems(
                    TimestampsToReturn.Both,
                    listOf(request)
                )

                val items = itemsFuture.get(OPERATION_TIMEOUT, TimeUnit.MILLISECONDS)

                items.forEach { item ->
                    item.setValueConsumer { _, dataValue ->
                        try {
                            // Track data received time
                            lastDataReceived[nodeIdString] = System.currentTimeMillis()

                            // Reset connection lost flag
                            if (isConnectionLost.getAndSet(false)) {
                                Log.d("OPCUAClient", "✅ Connection restored - data received")
                            }

                            onValueChange(dataValue)
                        } catch (e: Exception) {
                            Log.w("OPCUAClient", "Error in value consumer", e)
                        }
                    }
                }
            }
        } catch (e: Exception) {
            Log.e("OPCUAClient", "❌ Subscription error", e)

            // Check if it's a connection error
            if (e.message?.contains("Bad_", ignoreCase = true) == true ||
                e.message?.contains("timeout", ignoreCase = true) == true) {
                handleConnectionLost("Subscription failed: ${e.message}")
            }
            throw e
        }
    }

    /**
     * Write node với connection error detection
     */
    suspend fun writeNode(nodeIdString: String, rawValue: Any): StatusCode? = withContext(Dispatchers.IO) {
        val cli = client ?: return@withContext null

        try {
            withTimeout(OPERATION_TIMEOUT) {
                val nodeId = NodeId.parse(nodeIdString)
                val variant = when (rawValue) {
                    is Boolean -> Variant(rawValue)
                    is Int -> Variant(rawValue)
                    is Short -> Variant(rawValue)
                    else -> throw IllegalArgumentException("Unsupported type: ${rawValue.javaClass.simpleName}")
                }

                val dataValue = DataValue(variant, null, null)

                val statusFuture = cli.writeValue(nodeId, dataValue)
                val status = statusFuture.get(OPERATION_TIMEOUT, TimeUnit.MILLISECONDS)

                if (status.isGood) {
                    // Reset connection lost on successful write
                    isConnectionLost.set(false)
                    lastSessionActiveTime = System.currentTimeMillis() // Update active time
                    Log.d("OPCUAClient", "✅ Write successful: $nodeIdString = $rawValue")
                } else {
                    Log.e("OPCUAClient", "❌ Write failed: $nodeIdString, Status: $status")

                    // Check for connection-related errors
                    if (status.value == StatusCodes.Bad_NotConnected ||
                        status.value == StatusCodes.Bad_ConnectionClosed ||
                        status.value == StatusCodes.Bad_SessionClosed) {
                        handleConnectionLost("Write failed with connection error: $status")
                    }
                }
                status
            }
        } catch (e: Exception) {
            Log.e("OPCUAClient", "❌ Write error", e)

            // Check if it's a connection error
            if (e.message?.contains("Bad_", ignoreCase = true) == true ||
                e.message?.contains("timeout", ignoreCase = true) == true ||
                e.message?.contains("connection", ignoreCase = true) == true) {
                handleConnectionLost("Write failed: ${e.message}")
            }
            null
        }
    }

    /**
     * Read access level của node
     */
    suspend fun readAccessLevel(nodeIdString: String): UByte? = withContext(Dispatchers.IO) {
        val cli = client ?: return@withContext null

        try {
            withTimeout(OPERATION_TIMEOUT) {
                val nodeId = NodeId.parse(nodeIdString)
                val readValueId = ReadValueId(nodeId, AttributeId.AccessLevel.uid(), null, null)
                val responseFuture = cli.read(0.0, TimestampsToReturn.Neither, listOf(readValueId))
                val response = responseFuture.get(OPERATION_TIMEOUT, TimeUnit.MILLISECONDS)
                val dataValue = response.results?.firstOrNull()
                dataValue?.value?.value as? UByte
            }
        } catch (e: Exception) {
            Log.e("OPCUAClient", "Failed to read AccessLevel for $nodeIdString", e)
            null
        }
    }

    /**
     * Check if connected
     */
    fun isConnected(): Boolean {
        if (isConnectionLost.get()) {
            return false
        }

        return try {
            client != null
        } catch (e: Exception) {
            false
        }
    }

    /**
     * Disconnect với cleanup
     */
    suspend fun disconnect() = withContext(Dispatchers.IO) {
        try {
            Log.d("OPCUAClient", "🔌 Disconnecting...")

            // Stop monitor
            connectionMonitorJob?.cancel()

            // Clean subscription
            subscription?.let { sub ->
                try {
                    withTimeout(2000L) {
                        val deleteFuture = sub.deleteMonitoredItems(sub.monitoredItems)
                        deleteFuture.get(2000, TimeUnit.MILLISECONDS)
                    }
                } catch (e: Exception) {
                    Log.w("OPCUAClient", "Error deleting monitored items", e)
                }
            }
            subscription = null

            // Disconnect client
            client?.let { cli ->
                try {
                    withTimeout(2000L) {
                        val disconnectFuture = cli.disconnect()
                        disconnectFuture.get(2000, TimeUnit.MILLISECONDS)
                    }
                } catch (e: Exception) {
                    Log.w("OPCUAClient", "Error during disconnect", e)
                }
            }

            client = null
            isConnectionLost.set(false)
            connectionLostCallback = null
            lastDataReceived.clear()

            Log.d("OPCUAClient", "✅ Disconnected")
        } catch (e: Exception) {
            Log.e("OPCUAClient", "❌ Error during disconnect", e)
        }
    }

    /**
     * Force cleanup on app exit
     */
    fun cleanup() {
        connectionMonitorJob?.cancel()
        monitorScope.cancel()
    }
}