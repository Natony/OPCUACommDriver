package com.example.s7opcuaapp.util

import java.util.concurrent.ConcurrentHashMap
import java.util.concurrent.locks.ReentrantReadWriteLock
import kotlin.concurrent.read
import kotlin.concurrent.write
import javax.inject.Inject
import javax.inject.Singleton

/**
 * Thread-safe configuration for button locking behavior
 */
@Singleton
class ButtonLockConfig @Inject constructor(
    private val statusLockConfig: StatusLockConfig
) {

    // Button types for easier management
    enum class ButtonType {
        BOOL_MANUAL,    // Manual mode bool buttons (0-3)
        BOOL_AUTO,      // Auto mode bool buttons (6-9)
        INT_AUTO,       // Auto mode int buttons (3-4 with offset)
        BOOL_COMMON,    // Common bool buttons (4,5,10,11,12,13,14)
        INT_COMMON      // Common int buttons
    }

    // THREAD-SAFE: Use ConcurrentHashMap for thread-safe operations
    private val buttonGroups = ConcurrentHashMap<String, Set<Int>>().apply {
        put("manual_movement", setOf(0, 1, 2, 3))
        put("auto_pallets", setOf(6, 7, 8, 9))
        put("auto_int_pallets", setOf(203, 204))
        put("power_emergency", setOf(4, 10))
        put("mode_switch", setOf(11))
        put("direction", setOf(13))
        put("count_pallet", setOf(14))
    }

    private val crossLockRules = ConcurrentHashMap<Int, Set<Int>>().apply {
        // When any manual movement is active, lock auto operations
        put(0, setOf(6, 7, 8, 9, 203, 204))
        put(1, setOf(6, 7, 8, 9, 203, 204))
        put(2, setOf(6, 7, 8, 9, 203, 204))
        put(3, setOf(6, 7, 8, 9, 203, 204))
        put(14, setOf(0, 1, 2, 3, 203, 204))

        // When any auto operation is active, lock manual movements
        put(6, setOf(0, 1, 2, 3, 14, 203, 204))
        put(7, setOf(0, 1, 2, 3, 14, 203, 204))
        put(8, setOf(0, 1, 2, 3, 14, 203, 204))
        put(9, setOf(0, 1, 2, 3, 14, 203, 204))

        put(203, setOf(0, 1, 2, 3, 6, 7, 8, 9, 14))
        put(204, setOf(0, 1, 2, 3, 6, 7, 8, 9, 14))

        // When emergency stop is active, lock all operations
        put(10, setOf(0, 1, 2, 3, 6, 7, 8, 9, 14, 202, 203, 204))
    }

    // THREAD-SAFE: Use concurrent set for priority buttons
    private val priorityButtons = ConcurrentHashMap.newKeySet<Int>().apply {
        add(10) // Emergency stop has highest priority
    }

    // THREAD-SAFE: ReadWriteLock for complex operations
    private val configLock = ReentrantReadWriteLock()

    /**
     * THREAD-SAFE: Get locked buttons with read lock
     */
    fun getLockedButtons(activeButtons: Set<Int>, busyButtons: Set<Int>): Set<Int> {
        configLock.read {
            val locked = mutableSetOf<Int>()
            val activeOrBusy = activeButtons + busyButtons

            // Check each active/busy button
            for (buttonIndex in activeOrBusy) {
                // Skip if this is a priority button that's just busy (not active)
                if (buttonIndex in priorityButtons && buttonIndex !in activeButtons) {
                    continue
                }

                // Add group locks
                buttonGroups.forEach { (_, group) ->
                    if (buttonIndex in group) {
                        // Lock all other buttons in the same group
                        locked.addAll(group - buttonIndex)
                    }
                }

                // Add cross-lock rules
                crossLockRules[buttonIndex]?.let { toLock ->
                    locked.addAll(toLock - buttonIndex)
                }
            }

            // Remove priority buttons from locked set if they're not already active
            if (activeButtons.isNotEmpty()) {
                locked.removeAll(priorityButtons - activeButtons)
            }

            // Ensure no button locks itself
            locked.removeAll(activeOrBusy)

            return locked.toSet() // Return immutable copy
        }
    }

    /**
     * THREAD-SAFE: Add button group with write lock
     */
    fun addButtonGroup(groupName: String, buttons: Set<Int>) {
        configLock.write {
            buttonGroups[groupName] = buttons.toSet() // Store immutable copy
        }
    }

    /**
     * THREAD-SAFE: Add cross lock rule with write lock
     */
    fun addCrossLockRule(triggerButton: Int, lockedButtons: Set<Int>) {
        configLock.write {
            crossLockRules[triggerButton] = lockedButtons.toSet() // Store immutable copy
        }
    }

    /**
     * THREAD-SAFE: Update priority button
     */
    fun setPriorityButton(buttonIndex: Int, isPriority: Boolean) {
        if (isPriority) {
            priorityButtons.add(buttonIndex)
        } else {
            priorityButtons.remove(buttonIndex)
        }
    }

    /**
     * THREAD-SAFE: Check if button can interrupt
     */
    fun canInterrupt(buttonA: Int, buttonB: Int): Boolean {
        return buttonA in priorityButtons && buttonB !in priorityButtons
    }

    /**
     * THREAD-SAFE: Get button type
     */
    fun getButtonType(index: Int): ButtonType {
        return when (index) {
            in 0..3 -> ButtonType.BOOL_MANUAL
            in 6..9 -> ButtonType.BOOL_AUTO
            in 203..204 -> ButtonType.INT_AUTO
            4, 5, 10, 11, 12, 13, 14 -> ButtonType.BOOL_COMMON
            else -> ButtonType.INT_COMMON
        }
    }

    /**
     * THREAD-SAFE: Get button group name
     */
    fun getButtonGroup(index: Int): String? {
        return buttonGroups.entries.find { (_, group) -> index in group }?.key
    }

    /**
     * THREAD-SAFE: Get all button groups (immutable copy)
     */
    fun getAllButtonGroups(): Map<String, Set<Int>> {
        return buttonGroups.toMap() // Return immutable copy
    }

    /**
     * THREAD-SAFE: Get all cross lock rules (immutable copy)
     */
    fun getAllCrossLockRules(): Map<Int, Set<Int>> {
        return crossLockRules.toMap() // Return immutable copy
    }

    /**
     * THREAD-SAFE: Clear all configurations
     */
    fun clearAll() {
        configLock.write {
            buttonGroups.clear()
            crossLockRules.clear()
            priorityButtons.clear()
        }
    }

    /**
     * THREAD-SAFE: Export configuration
     */
    fun exportConfig(): ButtonLockConfiguration {
        configLock.read {
            return ButtonLockConfiguration(
                groups = buttonGroups.toMap(),
                crossLocks = crossLockRules.toMap(),
                priorities = priorityButtons.toSet()
            )
        }
    }

    /**
     * THREAD-SAFE: Import configuration
     */
    fun importConfig(config: ButtonLockConfiguration) {
        configLock.write {
            buttonGroups.clear()
            buttonGroups.putAll(config.groups)

            crossLockRules.clear()
            crossLockRules.putAll(config.crossLocks)

            priorityButtons.clear()
            priorityButtons.addAll(config.priorities)
        }
    }

    /**
     * Configuration data class for import/export
     */
    data class ButtonLockConfiguration(
        val groups: Map<String, Set<Int>>,
        val crossLocks: Map<Int, Set<Int>>,
        val priorities: Set<Int>
    )
}