package com.example.s7opcuaapp.di

import android.content.Context
import com.example.s7opcuaapp.data.local.AppDatabase
import com.example.s7opcuaapp.data.local.PrefsManager
import com.example.s7opcuaapp.data.local.dao.*
import dagger.Module
import dagger.Provides
import dagger.hilt.InstallIn
import dagger.hilt.android.qualifiers.ApplicationContext
import dagger.hilt.components.SingletonComponent
import javax.inject.Singleton

@Module
@InstallIn(SingletonComponent::class)
object DatabaseModule {

    @Provides
    @Singleton
    fun provideAppDatabase(
        @ApplicationContext context: Context
    ): AppDatabase {
        return AppDatabase.getInstance(context)
    }

    @Provides
    fun provideUserDao(database: AppDatabase): UserDao {
        return database.userDao()
    }

    @Provides
    fun provideLoginHistoryDao(database: AppDatabase): LoginHistoryDao {
        return database.loginHistoryDao()
    }

    @Provides
    fun provideDeviceAccessLogDao(database: AppDatabase): DeviceAccessLogDao {
        return database.deviceAccessLogDao()
    }

    @Provides
    fun provideDeviceDao(database: AppDatabase): DeviceDao {
        return database.deviceDao()
    }

    @Provides
    @Singleton
    fun providePrefsManager(
        @ApplicationContext context: Context
    ): PrefsManager {
        return PrefsManager(context)
    }
}