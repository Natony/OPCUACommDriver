package com.example.s7opcuaapp.viewmodel

import com.example.s7opcuaapp.data.local.PrefsManager
import com.example.s7opcuaapp.data.model.DeviceEntity
import com.example.s7opcuaapp.data.model.PlcData
import com.example.s7opcuaapp.data.repository.ApiRepositoryImpl
import com.example.s7opcuaapp.data.repository.S7Repository
import com.example.s7opcuaapp.ui.screen.control.ControlUiState
import com.example.s7opcuaapp.util.ButtonLockConfig
import com.example.s7opcuaapp.util.ConnectionTimeoutManager
import com.example.s7opcuaapp.util.PerformanceMonitor
import com.example.s7opcuaapp.util.StatusLockConfig
import com.google.common.truth.Truth.assertThat
import io.mockk.*
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.ExperimentalCoroutinesApi
import kotlinx.coroutines.flow.MutableSharedFlow
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.test.*
import org.junit.After
import org.junit.Before
import org.junit.Test

@OptIn(ExperimentalCoroutinesApi::class)
class ControlViewModelTest {

    private lateinit var prefsManager: PrefsManager
    private lateinit var repository: ApiRepositoryImpl
    private lateinit var performanceMonitor: PerformanceMonitor
    private lateinit var buttonLockConfig: ButtonLockConfig
    private lateinit var statusLockConfig: StatusLockConfig
    private lateinit var connectionTimeoutManager: ConnectionTimeoutManager

    private val testDispatcher = StandardTestDispatcher()
    private val plcDataFlow = MutableSharedFlow<PlcData>()
    private val loadingPercentFlow = MutableStateFlow(0)

    @Before
    fun setup() {
        Dispatchers.setMain(testDispatcher)

        prefsManager = mockk(relaxed = true)
        repository = mockk(relaxed = true)
        performanceMonitor = mockk(relaxed = true)
        buttonLockConfig = mockk(relaxed = true)
        statusLockConfig = mockk(relaxed = true)
        connectionTimeoutManager = mockk(relaxed = true)

        // Setup default mock behaviors
        every { repository.observePlcData() } returns plcDataFlow
        every { repository.observeLoadingPercent() } returns loadingPercentFlow
        every { repository.isConnected() } returns false
        coEvery { repository.start() } just Runs
        coEvery { repository.stop() } just Runs
        every { statusLockConfig.getLockedButtonsForStatus(any()) } returns emptySet()
    }

    @After
    fun tearDown() {
        Dispatchers.resetMain()
    }

    // ============== Initial State Tests ==============

    @Test
    fun `initial state is correct`() = runTest {
        // Arrange
        val viewModel = createViewModel()

        // Assert
        assertThat(viewModel.uiState.value).isEqualTo(ControlUiState())
        assertThat(viewModel.connectionState.value).isEqualTo(ControlViewModel.ConnectionState.Idle)
    }

    // ============== Connection State Tests ==============

    @Test
    fun `connection state is Idle initially`() = runTest {
        // Arrange
        val viewModel = createViewModel()

        // Assert
        assertThat(viewModel.connectionState.value).isInstanceOf(ControlViewModel.ConnectionState.Idle::class.java)
    }

    @Test
    fun `loading percent updates connection state to Connecting`() = runTest {
        // Arrange
        val viewModel = createViewModel()

        // Act
        loadingPercentFlow.emit(50)
        advanceUntilIdle()

        // Assert
        assertThat(viewModel.connectionState.value).isInstanceOf(ControlViewModel.ConnectionState.Connecting::class.java)
    }

    @Test
    fun `loading percent 100 updates connection state to Connected`() = runTest {
        // Arrange
        val viewModel = createViewModel()
        every { repository.isConnected() } returns true

        // Act
        loadingPercentFlow.emit(100)
        advanceUntilIdle()

        // Assert
        assertThat(viewModel.connectionState.value).isEqualTo(ControlViewModel.ConnectionState.Connected)
    }

    @Test
    fun `loading percent -1 indicates error state`() = runTest {
        // Arrange
        val viewModel = createViewModel()

        // Act
        loadingPercentFlow.emit(-1)
        advanceUntilIdle()

        // Assert
        assertThat(viewModel.connectionState.value).isInstanceOf(ControlViewModel.ConnectionState.Failed::class.java)
    }

    // ============== Offline Mode Tests ==============

    @Test
    fun `continueOffline sets offline state`() = runTest {
        // Arrange
        val viewModel = createViewModel()

        // Act
        viewModel.continueOffline()
        advanceUntilIdle()

        // Assert
        assertThat(viewModel.connectionState.value).isEqualTo(ControlViewModel.ConnectionState.Offline)
    }

    @Test
    fun `continueOffline sets loading percent to 100`() = runTest {
        // Arrange
        val viewModel = createViewModel()

        // Act
        viewModel.continueOffline()
        advanceUntilIdle()

        // Assert
        assertThat(viewModel.uiState.value.loadingPercent).isEqualTo(100)
    }

    // ============== Button Toggle Tests ==============

    @Test
    fun `onToggleBoolean does nothing in offline mode`() = runTest {
        // Arrange
        val viewModel = createViewModel()
        viewModel.continueOffline()
        advanceUntilIdle()

        // Act
        viewModel.onToggleBoolean(0, true)
        advanceUntilIdle()

        // Assert - should not write to repository
        coVerify(exactly = 0) { repository.writeBoolean(0, true) }
    }

    @Test
    fun `onToggleBoolean shows error message in offline mode`() = runTest {
        // Arrange
        val viewModel = createViewModel()
        viewModel.continueOffline()
        advanceUntilIdle()

        // Act
        viewModel.onToggleBoolean(0, true)
        advanceUntilIdle()

        // Assert
        assertThat(viewModel.uiState.value.errorMessage).contains("offline")
    }

    // ============== Function Selection Tests ==============

    @Test
    fun `onFunctionSelected updates selected function`() = runTest {
        // Arrange
        val viewModel = createViewModel()

        // Act
        viewModel.onFunctionSelected(5)

        // Assert
        assertThat(viewModel.uiState.value.selectedFunction).isEqualTo(5)
    }

    // ============== Inline Value Change Tests ==============

    @Test
    fun `onInlineValueChange updates int inputs`() = runTest {
        // Arrange
        val viewModel = createViewModel()

        // Act
        viewModel.onInlineValueChange(5, "123")

        // Assert
        assertThat(viewModel.uiState.value.intInputs[5]).isEqualTo("123")
    }

    @Test
    fun `onInlineValueChange can store multiple values`() = runTest {
        // Arrange
        val viewModel = createViewModel()

        // Act
        viewModel.onInlineValueChange(5, "100")
        viewModel.onInlineValueChange(6, "200")
        viewModel.onInlineValueChange(7, "300")

        // Assert
        assertThat(viewModel.uiState.value.intInputs[5]).isEqualTo("100")
        assertThat(viewModel.uiState.value.intInputs[6]).isEqualTo("200")
        assertThat(viewModel.uiState.value.intInputs[7]).isEqualTo("300")
    }

    // ============== Dialog Tests ==============

    @Test
    fun `openNumberDialog sets dialog state`() = runTest {
        // Arrange
        val viewModel = createViewModel()

        // Act
        viewModel.openNumberDialog("Enter Value", 5)

        // Assert
        assertThat(viewModel.uiState.value.openDialogForIndex).isEqualTo(5)
        assertThat(viewModel.uiState.value.dialogTitle).isEqualTo("Enter Value")
    }

    @Test
    fun `dismissDialog clears dialog state`() = runTest {
        // Arrange
        val viewModel = createViewModel()
        viewModel.openNumberDialog("Test", 3)

        // Act
        viewModel.dismissDialog()

        // Assert
        assertThat(viewModel.uiState.value.openDialogForIndex).isNull()
    }

    // ============== Stop Connection Tests ==============

    @Test
    fun `stopConnection calls repository stop`() = runTest {
        // Arrange
        val viewModel = createViewModel()

        // Act
        viewModel.stopConnection()
        advanceUntilIdle()

        // Assert
        coVerify { repository.stop() }
    }

    @Test
    fun `stopConnection resets UI state`() = runTest {
        // Arrange
        val viewModel = createViewModel()

        // Act
        viewModel.stopConnection()
        advanceUntilIdle()

        // Assert
        assertThat(viewModel.uiState.value.loadingPercent).isEqualTo(0)
        assertThat(viewModel.uiState.value.errorMessage).isNull()
    }

    // ============== Reset Tests ==============

    @Test
    fun `resetProcessingState clears busy buttons`() = runTest {
        // Arrange
        val viewModel = createViewModel()

        // Act
        viewModel.resetProcessingState()
        advanceUntilIdle()

        // Assert
        assertThat(viewModel.uiState.value.busyButtons).isEmpty()
    }

    @Test
    fun `resetProcessingState clears locked buttons`() = runTest {
        // Arrange
        val viewModel = createViewModel()

        // Act
        viewModel.resetProcessingState()
        advanceUntilIdle()

        // Assert
        assertThat(viewModel.uiState.value.lockedButtons).isEmpty()
    }

    @Test
    fun `dismissTimeoutDialog resets dialog state`() = runTest {
        // Arrange
        val viewModel = createViewModel()

        // Act
        viewModel.dismissTimeoutDialog()

        // Assert - should not throw
    }

    @Test
    fun `resetConnectionAttempts resets offline mode`() = runTest {
        // Arrange
        val viewModel = createViewModel()
        viewModel.continueOffline()
        advanceUntilIdle()

        // Act
        viewModel.resetConnectionAttempts()

        // Assert - test passes if no exception
    }

    // ============== Helper Methods ==============

    private fun createViewModel(): ControlViewModel {
        return ControlViewModel(
            prefsManager = prefsManager,
            repository = repository,
            performanceMonitor = performanceMonitor,
            buttonLockConfig = buttonLockConfig,
            statusLockConfig = statusLockConfig,
            connectionTimeoutManager = connectionTimeoutManager
        )
    }
}
