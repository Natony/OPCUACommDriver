package com.example.s7opcuaapp.ui.navigation

import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.hilt.navigation.compose.hiltViewModel
import androidx.navigation.NavHostController
import androidx.navigation.compose.NavHost
import androidx.navigation.compose.composable
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.example.s7opcuaapp.ui.screen.alarm.AlarmScreen
import com.example.s7opcuaapp.ui.screen.config.ConfigScreen
import com.example.s7opcuaapp.ui.screen.login.LoginScreen
import com.example.s7opcuaapp.viewmodel.ConfigViewModel
import com.example.s7opcuaapp.viewmodel.LoginViewModel

@Composable
fun RootNavHost(navController: NavHostController) {
    NavHost(
        navController = navController,
        startDestination = "login"
    ) {
        // 1. Login Screen
        composable("login") {
            val loginViewModel: LoginViewModel = hiltViewModel()
            val uiState by loginViewModel.uiState.collectAsStateWithLifecycle()

            LoginScreen(
                uiState = uiState,
                onUsernameChanged = { username -> loginViewModel.onUsernameChanged(username) },
                onPasswordChanged = { password -> loginViewModel.onPasswordChanged(password) },
                onTogglePasswordVisibility = { loginViewModel.onTogglePasswordVisibility() },
                onLoginClicked = {
                    loginViewModel.onLoginClicked {
                        // After successful login, go to config_select
                        navController.navigate("config_select") {
                            popUpTo("login") { inclusive = true }
                        }
                    }
                }
            )
        }

        // 2. ConfigSelect Screen (chọn device lần đầu)
        composable("config_select") {
            val configViewModel: ConfigViewModel = hiltViewModel()
            val uiState by configViewModel.uiState.collectAsStateWithLifecycle()

            ConfigScreen(
                uiState = uiState,
                onNewDeviceNameChanged = { deviceName -> configViewModel.onNewDeviceNameChanged(deviceName) },
                onNewDeviceIpChanged = { deviceIp -> configViewModel.onNewDeviceIpChanged(deviceIp) },
                onNewDevicePortChanged = { devicePort -> configViewModel.onNewDevicePortChanged(devicePort) },
                onNewDeviceUsernameChanged = { deviceUsername -> configViewModel.onNewDeviceUsernameChanged(deviceUsername) },
                onNewDevicePasswordChanged = { devicePassword -> configViewModel.onNewDevicePasswordChanged(devicePassword) },
                onAddDevice = { configViewModel.onAddDevice() },
                onRemoveDevice = { device -> configViewModel.onRemoveDevice(device) },
                onSelectDevice = { device ->
                    configViewModel.onSelectDevice(device) {
                        // After selecting device, go to main
                        navController.navigate("main") {
                            popUpTo("config_select") { inclusive = true }
                        }
                    }
                },
                onEditDevice = { device -> configViewModel.onEditDevice(device) },
                onCancelEdit = { configViewModel.onCancelEdit() }
            )
        }

        // 3. MainNavGraph (có TopNavigationBar)
        composable("main") {
            MainNavGraph(rootNavController = navController)
        }

        composable("alarm") {
            AlarmScreen(
                navController = navController
            )
        }
    }
}