package com.example.s7opcuaapp.data.api

import android.util.Log
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.*
import okhttp3.OkHttpClient
import okhttp3.logging.HttpLoggingInterceptor
import retrofit2.Retrofit
import retrofit2.converter.gson.GsonConverterFactory
import java.util.concurrent.TimeUnit

/**
 * Main API client that manages both REST API and SignalR connections
 */
class PlcApiClient(
    private val serverUrl: String
) {
    companion object {
        private const val TAG = "PlcApiClient"
        private const val CONNECT_TIMEOUT = 30L
        private const val READ_TIMEOUT = 30L
        private const val WRITE_TIMEOUT = 30L
    }

    private val scope = CoroutineScope(Dispatchers.IO + SupervisorJob())

    // HTTP client
    private val okHttpClient: OkHttpClient by lazy {
        val logging = HttpLoggingInterceptor { message ->
            Log.d(TAG, message)
        }.apply {
            level = HttpLoggingInterceptor.Level.BASIC
        }

        OkHttpClient.Builder()
            .connectTimeout(CONNECT_TIMEOUT, TimeUnit.SECONDS)
            .readTimeout(READ_TIMEOUT, TimeUnit.SECONDS)
            .writeTimeout(WRITE_TIMEOUT, TimeUnit.SECONDS)
            .addInterceptor(logging)
            .build()
    }

    // Retrofit instance
    private val retrofit: Retrofit by lazy {
        Retrofit.Builder()
            .baseUrl(serverUrl)
            .client(okHttpClient)
            .addConverterFactory(GsonConverterFactory.create())
            .build()
    }

    // API service
    val apiService: PlcApiService by lazy {
        retrofit.create(PlcApiService::class.java)
    }

    // SignalR client
    val signalRClient: PlcSignalRClient by lazy {
        PlcSignalRClient(serverUrl)
    }

    // Connection state
    private val _isConnected = MutableStateFlow(false)
    val isConnected: StateFlow<Boolean> = _isConnected

    // Current PLC ID
    private var currentPlcId: String? = null

    // Callbacks
    private var onConnectionLost: (() -> Unit)? = null
    private var onConnectionRestored: (() -> Unit)? = null

    /**
     * Set connection callbacks
     */
    fun setConnectionCallbacks(
        onLost: (() -> Unit)? = null,
        onRestored: (() -> Unit)? = null
    ) {
        onConnectionLost = onLost
        onConnectionRestored = onRestored
    }

    /**
     * Connect to the server
     */
    suspend fun connect(plcId: String? = null): Boolean = withContext(Dispatchers.IO) {
        try {
            Log.d(TAG, "Connecting to server: $serverUrl")
            currentPlcId = plcId

            // First check if server is reachable via REST
            val statusResponse = apiService.getPlcStatus()
            if (!statusResponse.isSuccessful) {
                Log.e(TAG, "Server not reachable: ${statusResponse.code()}")
                return@withContext false
            }

            // If plcId specified, connect to that PLC
            if (plcId != null) {
                val connectResponse = apiService.connectPlc(plcId)
                if (!connectResponse.isSuccessful || connectResponse.body()?.success != true) {
                    Log.e(TAG, "Failed to connect to PLC: $plcId")
                    return@withContext false
                }
            }

            // Connect to SignalR for real-time updates
            val signalRConnected = signalRClient.connect()
            if (!signalRConnected) {
                Log.w(TAG, "SignalR connection failed, will use polling")
            }

            // Subscribe to specific PLC if specified
            if (plcId != null) {
                signalRClient.subscribeToPlc(plcId)
            }

            _isConnected.value = true
            onConnectionRestored?.invoke()
            Log.d(TAG, "Connected successfully")
            true

        } catch (e: Exception) {
            Log.e(TAG, "Connection failed", e)
            _isConnected.value = false
            onConnectionLost?.invoke()
            false
        }
    }

    /**
     * Disconnect from the server
     */
    suspend fun disconnect() = withContext(Dispatchers.IO) {
        try {
            Log.d(TAG, "Disconnecting...")

            // Disconnect PLC if connected
            currentPlcId?.let { plcId ->
                try {
                    apiService.disconnectPlc(plcId)
                } catch (e: Exception) {
                    Log.w(TAG, "Error disconnecting PLC", e)
                }
            }

            // Disconnect SignalR
            signalRClient.disconnect()

            currentPlcId = null
            _isConnected.value = false
            Log.d(TAG, "Disconnected")

        } catch (e: Exception) {
            Log.e(TAG, "Error during disconnect", e)
        }
    }

    /**
     * Read all tags from current PLC
     */
    suspend fun readAllTags(): List<TagDto> = withContext(Dispatchers.IO) {
        try {
            val response = if (currentPlcId != null) {
                apiService.getTagsByPlc(currentPlcId!!)
            } else {
                apiService.getAllTags()
            }

            if (response.isSuccessful && response.body()?.success == true) {
                response.body()?.data ?: emptyList()
            } else {
                Log.e(TAG, "Failed to read tags: ${response.body()?.error}")
                emptyList()
            }
        } catch (e: Exception) {
            Log.e(TAG, "Error reading tags", e)
            emptyList()
        }
    }

    /**
     * Write a boolean value
     */
    suspend fun writeBoolean(nodeId: String, value: Boolean): Boolean = withContext(Dispatchers.IO) {
        writeValue(nodeId, value)
    }

    /**
     * Write an integer value
     */
    suspend fun writeInt(nodeId: String, value: Int): Boolean = withContext(Dispatchers.IO) {
        writeValue(nodeId, value)
    }

    /**
     * Write a value to a tag
     */
    private suspend fun writeValue(nodeId: String, value: Any): Boolean {
        val plcId = currentPlcId ?: return false

        return try {
            // URL encode the nodeId
            val encodedNodeId = java.net.URLEncoder.encode(nodeId, "UTF-8")

            val response = apiService.writeTag(
                plcId = plcId,
                nodeId = encodedNodeId,
                request = WriteTagRequest(value)
            )

            if (response.isSuccessful && response.body()?.success == true) {
                Log.d(TAG, "Write successful: $nodeId = $value")
                true
            } else {
                Log.e(TAG, "Write failed: ${response.body()?.error}")
                false
            }
        } catch (e: Exception) {
            Log.e(TAG, "Error writing tag: $nodeId", e)
            false
        }
    }

    /**
     * Check connection health
     */
    suspend fun checkConnectionHealth(): Boolean = withContext(Dispatchers.IO) {
        try {
            val response = apiService.getPlcStatus()
            val isHealthy = response.isSuccessful

            if (isHealthy && !_isConnected.value) {
                _isConnected.value = true
                onConnectionRestored?.invoke()
            } else if (!isHealthy && _isConnected.value) {
                _isConnected.value = false
                onConnectionLost?.invoke()
            }

            isHealthy
        } catch (e: Exception) {
            if (_isConnected.value) {
                _isConnected.value = false
                onConnectionLost?.invoke()
            }
            false
        }
    }

    /**
     * Get tag value updates flow (from SignalR)
     */
    fun observeTagUpdates(): SharedFlow<TagValueUpdate> {
        return signalRClient.tagValueUpdates
    }

    /**
     * Get connection state updates flow
     */
    fun observeConnectionStatus(): SharedFlow<ConnectionStatusDto> {
        return signalRClient.connectionStatusUpdates
    }

    /**
     * Cleanup resources
     */
    fun cleanup() {
        signalRClient.cleanup()
        scope.cancel()
    }
}
