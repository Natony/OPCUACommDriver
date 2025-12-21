package com.example.s7opcuaapp.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.example.s7opcuaapp.data.local.PrefsManager
import com.example.s7opcuaapp.data.model.DeviceEntity
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.launch
import javax.inject.Inject

data class ConfigUiState(
    val deviceList: List<DeviceEntity> = emptyList(),
    val currentDevice: DeviceEntity? = null,
    val newDeviceName: String = "",
    val newDeviceIp: String = "",
    val newDevicePort: String = "4840",
    val newDeviceUsername: String = "",
    val newDevicePassword: String = "",
    val errorMessage: String? = null,
    val isEditMode: Boolean = false,
    val editingDevice: DeviceEntity? = null
)

@HiltViewModel
class ConfigViewModel @Inject constructor(
    private val prefsManager: PrefsManager
) : ViewModel() {

    private val _uiState = MutableStateFlow(ConfigUiState())
    val uiState: StateFlow<ConfigUiState> = _uiState

    init {
        loadDevices()
    }

    private fun loadDevices() {
        viewModelScope.launch {
            val list = prefsManager.getAllDevices()
            val current = prefsManager.getCurrentDevice()
            _uiState.value = _uiState.value.copy(
                deviceList = list,
                currentDevice = current
            )
        }
    }

    fun onNewDeviceNameChanged(new: String) {
        _uiState.value = _uiState.value.copy(newDeviceName = new)
    }

    fun onNewDeviceIpChanged(new: String) {
        _uiState.value = _uiState.value.copy(newDeviceIp = new)
    }

    fun onNewDevicePortChanged(new: String) {
        _uiState.value = _uiState.value.copy(newDevicePort = new)
    }

    fun onNewDeviceUsernameChanged(new: String) {
        _uiState.value = _uiState.value.copy(newDeviceUsername = new)
    }

    fun onNewDevicePasswordChanged(new: String) {
        _uiState.value = _uiState.value.copy(newDevicePassword = new)
    }

    fun onAddDevice() {
        val state = _uiState.value
        val name = state.newDeviceName.trim()
        val ip = state.newDeviceIp.trim()
        val portStr = state.newDevicePort.trim()
        val username = state.newDeviceUsername.trim()
        val password = state.newDevicePassword.trim()

        if (name.isEmpty() || ip.isEmpty() || portStr.isEmpty()) {
            _uiState.value = state.copy(errorMessage = "Tên, IP, Port không được để trống")
            return
        }

        val port = portStr.toIntOrNull()
        if (port == null || port <= 0) {
            _uiState.value = state.copy(errorMessage = "Port không hợp lệ")
            return
        }

        viewModelScope.launch {
            if (state.isEditMode && state.editingDevice != null) {
                // Update existing device
                val updatedDevice = state.editingDevice.copy(
                    name = name,
                    ipAddress = ip,
                    port = port,
                    opcUsername = username,
                    opcPassword = password
                )

                val updatedList = state.deviceList.map {
                    if (it.id == updatedDevice.id) updatedDevice else it
                }

                prefsManager.saveDeviceList(updatedList)

                // If this was the current device, update it
                if (state.currentDevice?.id == updatedDevice.id) {
                    prefsManager.setCurrentDevice(updatedDevice)
                }

                _uiState.value = state.copy(
                    deviceList = updatedList,
                    currentDevice = if (state.currentDevice?.id == updatedDevice.id) updatedDevice else state.currentDevice,
                    newDeviceName = "",
                    newDeviceIp = "",
                    newDevicePort = "4840",
                    newDeviceUsername = "",
                    newDevicePassword = "",
                    errorMessage = null,
                    isEditMode = false,
                    editingDevice = null
                )
            } else {
                // Add new device
                val id = System.currentTimeMillis().toString()
                val newDevice = DeviceEntity(
                    id = id,
                    name = name,
                    ipAddress = ip,
                    port = port,
                    opcUsername = username,
                    opcPassword = password,
                    useOpcUa = true
                )

                val updatedList = state.deviceList + newDevice
                prefsManager.saveDeviceList(updatedList)

                _uiState.value = state.copy(
                    deviceList = updatedList,
                    newDeviceName = "",
                    newDeviceIp = "",
                    newDevicePort = "4840",
                    newDeviceUsername = "",
                    newDevicePassword = "",
                    errorMessage = null
                )
            }
        }
    }

    fun onEditDevice(device: DeviceEntity) {
        _uiState.value = _uiState.value.copy(
            isEditMode = true,
            editingDevice = device,
            newDeviceName = device.name,
            newDeviceIp = device.ipAddress,
            newDevicePort = device.port.toString(),
            newDeviceUsername = device.opcUsername,
            newDevicePassword = device.opcPassword
        )
    }

    fun onCancelEdit() {
        _uiState.value = _uiState.value.copy(
            isEditMode = false,
            editingDevice = null,
            newDeviceName = "",
            newDeviceIp = "",
            newDevicePort = "4840",
            newDeviceUsername = "",
            newDevicePassword = "",
            errorMessage = null
        )
    }

    fun onRemoveDevice(device: DeviceEntity) {
        viewModelScope.launch {
            val updatedList = _uiState.value.deviceList.filterNot { it.id == device.id }
            prefsManager.saveDeviceList(updatedList)

            val current = prefsManager.getCurrentDevice()
            if (current?.id == device.id) {
                prefsManager.clearCurrentDevice()
                _uiState.value = _uiState.value.copy(
                    deviceList = updatedList,
                    currentDevice = null
                )
            } else {
                _uiState.value = _uiState.value.copy(deviceList = updatedList)
            }
        }
    }

    fun onSelectDevice(device: DeviceEntity, onSuccess: () -> Unit) {
        viewModelScope.launch {
            prefsManager.setCurrentDevice(device)
            _uiState.value = _uiState.value.copy(currentDevice = device)
            onSuccess()
        }
    }
}