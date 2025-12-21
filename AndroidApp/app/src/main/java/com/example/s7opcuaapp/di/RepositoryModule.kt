package com.example.s7opcuaapp.di

import com.example.s7opcuaapp.data.buffer.PlcDataBuffer
import com.example.s7opcuaapp.data.local.AppDatabase
import com.example.s7opcuaapp.data.local.PrefsManager
import com.example.s7opcuaapp.data.model.DeviceEntity
import com.example.s7opcuaapp.data.repository.*
import com.example.s7opcuaapp.util.ButtonLockConfig
import com.example.s7opcuaapp.util.PerformanceMonitor
import com.example.s7opcuaapp.util.StatusLockConfig
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object RepositoryModule {

    @Provides
    @Singleton
    fun providePerformanceMonitor(): PerformanceMonitor {
        return PerformanceMonitor()
    }

    @Provides
    @Singleton
    fun providePlcDataBuffer(
        performanceMonitor: PerformanceMonitor
    ): PlcDataBuffer {
        return PlcDataBuffer(performanceMonitor)
    }


    @Provides
    @Singleton
    fun provideStatusLockConfig(prefsManager: PrefsManager): StatusLockConfig {
        return StatusLockConfig(prefsManager)
    }

    @Provides
    @Singleton
    fun provideButtonLockConfig(statusLockConfig: StatusLockConfig): ButtonLockConfig {
        return ButtonLockConfig(statusLockConfig)
    }

    @Provides
    @Singleton
    fun provideUserRepository(
        database: AppDatabase,
        prefsManager: PrefsManager
    ): UserRepository {
        return UserRepositoryImpl(database, prefsManager)
    }

    @Provides
    @Singleton
    fun provideLogRepository(
        database: AppDatabase
    ): LogRepository {
        return LogRepositoryImpl(database)
    }

    /**
     * Provide S7Repository using API-based implementation
     * This connects through the WPF server instead of direct OPC UA
     */
    @Provides
    fun provideS7Repository(
        prefsManager: PrefsManager,
        dataBuffer: PlcDataBuffer,
        performanceMonitor: PerformanceMonitor
    ): S7Repository {
        val device = prefsManager.getCurrentDevice() ?: DeviceEntity(
            id = "default",
            name = "Default Device",
            ipAddress = "192.168.1.100",
            port = 4840,
            opcUsername = "",
            opcPassword = "",
            useOpcUa = true
        )

        // Use API-based repository (connects through WPF server)
        return ApiRepositoryImpl(
            device = device,
            dataBuffer = dataBuffer,
            performanceMonitor = performanceMonitor
        )
    }
}