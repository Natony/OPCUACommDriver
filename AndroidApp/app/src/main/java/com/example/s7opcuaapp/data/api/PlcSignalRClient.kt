package com.example.s7opcuaapp.data.api

import android.util.Log
import com.google.gson.Gson
import com.microsoft.signalr.HubConnection
import com.microsoft.signalr.HubConnectionBuilder
import com.microsoft.signalr.HubConnectionState
import kotlinx.coroutines.*
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.SharedFlow
import kotlinx.coroutines.flow.StateFlow

/**
 * SignalR client for real-time PLC data updates
 */
class PlcSignalRClient(
    private val baseUrl: String
) {
    companion object {
        private const val TAG = "PlcSignalR"
    }

    private var hubConnection: HubConnection? = null
    private val gson = Gson()
    private val scope = CoroutineScope(Dispatchers.IO + SupervisorJob())

    // Connection state
    private val _connectionState = MutableStateFlow(false)
    val connectionState: StateFlow<Boolean> = _connectionState

    // Tag value updates flow
    private val _tagValueUpdates = MutableSharedFlow<TagValueUpdate>(replay = 0, extraBufferCapacity = 100)
    val tagValueUpdates: SharedFlow<TagValueUpdate> = _tagValueUpdates

    // Connection state updates flow
    private val _connectionStatusUpdates = MutableSharedFlow<ConnectionStatusDto>(replay = 0, extraBufferCapacity = 10)
    val connectionStatusUpdates: SharedFlow<ConnectionStatusDto> = _connectionStatusUpdates

    // All tags update flow
    private val _allTagsUpdates = MutableSharedFlow<List<TagDto>>(replay = 1, extraBufferCapacity = 1)
    val allTagsUpdates: SharedFlow<List<TagDto>> = _allTagsUpdates

    /**
     * Connect to SignalR hub
     */
    suspend fun connect(): Boolean = withContext(Dispatchers.IO) {
        try {
            if (hubConnection?.connectionState == HubConnectionState.CONNECTED) {
                Log.d(TAG, "Already connected")
                return@withContext true
            }

            val hubUrl = "$baseUrl/hubs/plc"
            Log.d(TAG, "Connecting to SignalR hub: $hubUrl")

            hubConnection = HubConnectionBuilder.create(hubUrl)
                .build()

            // Setup event handlers
            setupEventHandlers()

            // Connect
            hubConnection?.start()?.blockingAwait()

            _connectionState.value = true
            Log.d(TAG, "Connected to SignalR hub")

            // Subscribe to all updates
            subscribeToAll()

            true
        } catch (e: Exception) {
            Log.e(TAG, "Failed to connect to SignalR hub", e)
            _connectionState.value = false
            false
        }
    }

    /**
     * Setup SignalR event handlers
     */
    private fun setupEventHandlers() {
        hubConnection?.apply {
            // Handle tag value changes
            on("TagValueChanged", { data: Any ->
                try {
                    val json = gson.toJson(data)
                    val update = gson.fromJson(json, TagValueUpdate::class.java)
                    scope.launch {
                        _tagValueUpdates.emit(update)
                    }
                } catch (e: Exception) {
                    Log.e(TAG, "Error parsing TagValueChanged", e)
                }
            }, Any::class.java)

            // Handle connection state changes
            on("ConnectionStateChanged", { data: Any ->
                try {
                    val json = gson.toJson(data)
                    val status = gson.fromJson(json, ConnectionStatusDto::class.java)
                    scope.launch {
                        _connectionStatusUpdates.emit(status)
                    }
                } catch (e: Exception) {
                    Log.e(TAG, "Error parsing ConnectionStateChanged", e)
                }
            }, Any::class.java)

            // Handle all tag values response
            on("AllTagValues", { data: Any ->
                try {
                    val json = gson.toJson(data)
                    val tags = gson.fromJson(json, Array<TagDto>::class.java).toList()
                    scope.launch {
                        _allTagsUpdates.emit(tags)
                    }
                } catch (e: Exception) {
                    Log.e(TAG, "Error parsing AllTagValues", e)
                }
            }, Any::class.java)

            // Handle PLC status response
            on("PlcStatus", { data: Any ->
                Log.d(TAG, "Received PlcStatus: $data")
            }, Any::class.java)

            // Handle write result
            on("WriteResult", { data: Any ->
                Log.d(TAG, "Write result: $data")
            }, Any::class.java)

            // Handle connection events
            onClosed { exception ->
                Log.w(TAG, "SignalR connection closed", exception)
                _connectionState.value = false

                // Auto-reconnect after delay
                scope.launch {
                    delay(5000)
                    if (_connectionState.value == false) {
                        Log.d(TAG, "Attempting to reconnect...")
                        connect()
                    }
                }
            }
        }
    }

    /**
     * Subscribe to all PLC updates
     */
    suspend fun subscribeToAll() {
        try {
            hubConnection?.invoke("SubscribeToAll")
            Log.d(TAG, "Subscribed to all PLCs")
        } catch (e: Exception) {
            Log.e(TAG, "Failed to subscribe to all", e)
        }
    }

    /**
     * Subscribe to a specific PLC
     */
    suspend fun subscribeToPlc(plcId: String) {
        try {
            hubConnection?.invoke("SubscribeToPlc", plcId)
            Log.d(TAG, "Subscribed to PLC: $plcId")
        } catch (e: Exception) {
            Log.e(TAG, "Failed to subscribe to PLC: $plcId", e)
        }
    }

    /**
     * Request all current tag values
     */
    suspend fun getAllTagValues() {
        try {
            hubConnection?.invoke("GetAllTagValues")
            Log.d(TAG, "Requested all tag values")
        } catch (e: Exception) {
            Log.e(TAG, "Failed to get all tag values", e)
        }
    }

    /**
     * Write a tag value via SignalR
     */
    suspend fun writeTag(plcId: String, nodeId: String, value: Any) {
        try {
            hubConnection?.invoke("WriteTag", plcId, nodeId, value)
            Log.d(TAG, "Write request sent: $nodeId = $value")
        } catch (e: Exception) {
            Log.e(TAG, "Failed to write tag: $nodeId", e)
        }
    }

    /**
     * Disconnect from SignalR hub
     */
    fun disconnect() {
        try {
            hubConnection?.stop()
            hubConnection = null
            _connectionState.value = false
            Log.d(TAG, "Disconnected from SignalR hub")
        } catch (e: Exception) {
            Log.e(TAG, "Error disconnecting from SignalR hub", e)
        }
    }

    /**
     * Check if connected
     */
    fun isConnected(): Boolean {
        return hubConnection?.connectionState == HubConnectionState.CONNECTED
    }

    /**
     * Cleanup resources
     */
    fun cleanup() {
        disconnect()
        scope.cancel()
    }
}
