package com.example.s7opcuaapp

import android.os.Bundle
import android.util.Log
import android.view.WindowManager
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.compose.runtime.LaunchedEffect
import androidx.lifecycle.lifecycleScope
import androidx.navigation.compose.rememberNavController
import com.example.s7opcuaapp.data.local.PrefsManager
import com.example.s7opcuaapp.data.model.UserRole
import com.example.s7opcuaapp.data.repository.UserRepository
import com.example.s7opcuaapp.ui.navigation.RootNavHost
import com.example.s7opcuaapp.ui.theme.S7Theme
import com.example.s7opcuaapp.util.PasswordUtils
import com.example.s7opcuaapp.util.PerformanceMonitor
import com.example.s7opcuaapp.util.SessionManager
import dagger.hilt.android.AndroidEntryPoint
import kotlinx.coroutines.launch
import kotlinx.coroutines.runBlocking
import javax.inject.Inject
import com.example.s7opcuaapp.data.buffer.PlcDataBuffer

@AndroidEntryPoint
class MainActivity : ComponentActivity() {

    @Inject
    lateinit var prefsManager: PrefsManager
    @Inject
    lateinit var sessionManager: SessionManager
    @Inject
    lateinit var userRepository: UserRepository
    @Inject
    lateinit var performanceMonitor: PerformanceMonitor
    @Inject
    lateinit var plcDataBuffer: PlcDataBuffer

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        // Enable hardware acceleration
        window.setFlags(
            WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED,
            WindowManager.LayoutParams.FLAG_HARDWARE_ACCELERATED
        )

        lifecycleScope.launch {
            initializeDefaultAdmin()
        }

        setContent {
            S7Theme {
                val navController = rememberNavController()

                // Check for existing session
                LaunchedEffect(Unit) {
                    if (sessionManager.validateSession()) {
                        // Session valid, check if device selected
                        if (prefsManager.getCurrentDevice() != null) {
                            navController.navigate("main") {
                                popUpTo("login") { inclusive = true }
                            }
                        } else {
                            navController.navigate("config_select") {
                                popUpTo("login") { inclusive = true }
                            }
                        }
                    }
                }

                RootNavHost(navController = navController)
            }
        }
    }

    private suspend fun initializeDefaultAdmin() {
        try {
            println("🔄 Checking for admin accounts...")

            val adminCount = userRepository.getActiveUserCountByRole(UserRole.ADMIN)
            println("📊 Current admin count: $adminCount")

            if (adminCount == 0) {
                println("🚀 Creating default admin account...")

                val plainPassword = "Tin123456"
                println("🔐 Creating admin with password: $plainPassword")

                // KHÔNG hash password ở đây, để UserRepository tự hash
                val result = userRepository.createUser(
                    username = "admin",
                    password = plainPassword, // Truyền plain password, UserRepository sẽ tự hash
                    role = UserRole.ADMIN,
                    createdBy = "system"
                )

                result.fold(
                    onSuccess = { user ->
                        println("✅ Admin account created successfully:")
                        println("   Username: ${user.username}")
                        println("   ID: ${user.id}")
                        println("   Role: ${user.role}")
                        println("   Active: ${user.isActive}")

                        // Verify bằng cách thử authenticate
                        val authResult = userRepository.authenticate("admin", "123456")
                        authResult.fold(
                            onSuccess = { authUser ->
                                println("✅ Admin account verification successful")
                            },
                            onFailure = { error ->
                                println("❌ Admin account verification failed: ${error.message}")
                            }
                        )
                    },
                    onFailure = { error ->
                        println("❌ Failed to create admin account: ${error.message}")
                        error.printStackTrace()
                    }
                )
            } else {
                println("✅ Admin account already exists, count: $adminCount")

                // Debug: Check existing admin và thử reset password
                val existingAdmin = userRepository.getUserByUsername("admin")
                if (existingAdmin != null) {
                    println("👤 Existing admin info:")
                    println("   Username: ${existingAdmin.username}")
                    println("   ID: ${existingAdmin.id}")
                    println("   Active: ${existingAdmin.isActive}")
                    println("   Role: ${existingAdmin.role}")

                    // FIX: Reset password để đảm bảo password đúng
                    println("🔧 Resetting admin password to ensure correct hash...")
                    val resetResult = userRepository.changePassword(
                        userId = existingAdmin.id,
                        newPassword = "Tin123456",
                        modifiedBy = "system"
                    )
                    resetResult.fold(
                        onSuccess = {
                            println("✅ Admin password reset successfully")

                            // Verify lại
                            val authResult = userRepository.authenticate("admin", "Tin123456")
                            authResult.fold(
                                onSuccess = { authUser ->
                                    println("✅ Admin password verification successful after reset")
                                },
                                onFailure = { error ->
                                    println("❌ Admin password verification still failed: ${error.message}")
                                }
                            )
                        },
                        onFailure = { error ->
                            println("❌ Failed to reset admin password: ${error.message}")
                        }
                    )
                }
            }
        } catch (e: Exception) {
            println("💥 Error in initializeDefaultAdmin: ${e.message}")
            e.printStackTrace()
        }
    }

    override fun onDestroy() {
        // Cleanup resources
        lifecycleScope.launch {
            try {
                // Stop all connections
                plcDataBuffer.dispose()
                performanceMonitor.cleanup()
            } catch (e: Exception) {
                Log.e("MainActivity", "Error during cleanup", e)
            }
        }
        super.onDestroy()
    }
}