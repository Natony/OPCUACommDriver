package com.example.s7opcuaapp.viewmodel

import androidx.lifecycle.ViewModel
import androidx.lifecycle.viewModelScope
import com.example.s7opcuaapp.util.PerformanceMonitor
import dagger.hilt.android.lifecycle.HiltViewModel
import kotlinx.coroutines.delay
import kotlinx.coroutines.flow.*
import kotlinx.coroutines.launch
import javax.inject.Inject

@HiltViewModel
class PerformanceViewModel @Inject constructor(
    private val performanceMonitor: PerformanceMonitor
) : ViewModel() {

    private val _performanceData = MutableStateFlow(
        PerformanceMonitor.PerformanceReport()
    )
    val performanceData: StateFlow<PerformanceMonitor.PerformanceReport> = _performanceData

    init {
        // Start periodic performance updates
        viewModelScope.launch {
            while (true) {
                _performanceData.value = performanceMonitor.generateReport()
                delay(1000) // Update every second
            }
        }
    }

    fun resetStats() {
        performanceMonitor.reset()
        _performanceData.value = PerformanceMonitor.PerformanceReport()
    }
}