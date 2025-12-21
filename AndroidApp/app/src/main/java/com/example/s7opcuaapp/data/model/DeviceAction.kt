package com.example.s7opcuaapp.data.model

enum class DeviceAction {
    CONNECT,
    DISCONNECT,
    READ,
    WRITE,
    WRITE_BOOL,
    WRITE_INT,
    FUNCTION_EXECUTE,
    CONFIG_CHANGE,
    LOGIN,
    LOGOUT
}