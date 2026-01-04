using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Models;
using Serilog;

namespace OpcUaCommunicationEngine.Services.Auth;

/// <summary>
/// Service for operator lock management
/// Ensures only one operator can control the system at a time
/// </summary>
public class OperatorLockService
{
    private readonly AuthSettings _settings;
    private readonly object _lock = new();
    private OperatorLock? _currentLock;
    private Timer? _timeoutTimer;
    private static ILogger Logger => Log.Logger;

    /// <summary>
    /// Event fired when lock is acquired
    /// </summary>
    public event EventHandler<LockEventArgs>? LockAcquired;

    /// <summary>
    /// Event fired when lock is released
    /// </summary>
    public event EventHandler<LockEventArgs>? LockReleased;

    /// <summary>
    /// Event fired when lock is extended
    /// </summary>
    public event EventHandler<LockEventArgs>? LockExtended;

    public OperatorLockService(AuthSettings settings)
    {
        _settings = settings;
        StartTimeoutChecker();
    }

    #region Public Methods

    /// <summary>
    /// Get current lock status
    /// </summary>
    public LockStatus GetLockStatus()
    {
        lock (_lock)
        {
            if (_currentLock == null || _currentLock.IsExpired)
            {
                if (_currentLock?.IsExpired == true)
                {
                    ReleaseLockInternal("timeout");
                }

                return new LockStatus { IsLocked = false };
            }

            return new LockStatus
            {
                IsLocked = true,
                LockId = _currentLock.LockId,
                UserId = _currentLock.UserId,
                Username = _currentLock.Username,
                DisplayName = _currentLock.DisplayName,
                AcquiredAt = _currentLock.AcquiredAt,
                ExpiresAt = _currentLock.ExpiresAt,
                TimeRemainingSeconds = (int)_currentLock.TimeRemaining.TotalSeconds
            };
        }
    }

    /// <summary>
    /// Try to acquire lock for a user
    /// </summary>
    public (bool success, OperatorLock? operatorLock, string? error, string? lockedByUsername, string? lockedByDisplayName)
        TryAcquireLock(string userId, string username, string displayName, UserRole role, int? durationMinutes = null)
    {
        lock (_lock)
        {
            // Check if user has permission to acquire lock
            if (role < UserRole.Operator)
            {
                return (false, null, "Insufficient permissions. Operator role required.", null, null);
            }

            // Check if there's an existing lock
            if (_currentLock != null && !_currentLock.IsExpired)
            {
                // If same user, refresh the lock
                if (_currentLock.UserId == userId)
                {
                    return ExtendLockInternal(userId, durationMinutes ?? _settings.LockTimeoutMinutes);
                }

                // Lock is held by another user
                return (false, null, "Lock is currently held by another operator",
                    _currentLock.Username, _currentLock.DisplayName);
            }

            // Clear expired lock if any
            if (_currentLock?.IsExpired == true)
            {
                ReleaseLockInternal("timeout");
            }

            // Calculate lock duration
            var duration = durationMinutes ?? _settings.LockTimeoutMinutes;
            var maxDuration = _settings.MaxLockDurationMinutes;
            duration = Math.Min(duration, maxDuration);

            // Acquire new lock
            _currentLock = new OperatorLock
            {
                LockId = Guid.NewGuid().ToString(),
                UserId = userId,
                Username = username,
                DisplayName = displayName,
                AcquiredAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddMinutes(duration),
                LastActivity = DateTime.UtcNow
            };

            Logger.Information("Lock acquired by {Username} (expires in {Duration} minutes)",
                username, duration);

            // Fire event
            LockAcquired?.Invoke(this, new LockEventArgs
            {
                Lock = _currentLock,
                Reason = "acquired"
            });

            return (true, _currentLock, null, null, null);
        }
    }

    /// <summary>
    /// Release lock (by lock holder)
    /// </summary>
    public (bool success, string? error) ReleaseLock(string userId)
    {
        lock (_lock)
        {
            if (_currentLock == null)
            {
                return (true, null); // No lock to release
            }

            if (_currentLock.UserId != userId)
            {
                return (false, "You do not hold the current lock");
            }

            ReleaseLockInternal("manual");
            return (true, null);
        }
    }

    /// <summary>
    /// Force release lock (admin only)
    /// </summary>
    public (bool success, string? error) ForceReleaseLock(string adminUserId, UserRole role)
    {
        lock (_lock)
        {
            if (role != UserRole.Admin)
            {
                return (false, "Only administrators can force release locks");
            }

            if (_currentLock == null)
            {
                return (true, null); // No lock to release
            }

            var previousHolder = _currentLock.Username;
            ReleaseLockInternal("forced");

            Logger.Warning("Lock force released by admin. Previous holder: {Username}", previousHolder);
            return (true, null);
        }
    }

    /// <summary>
    /// Extend current lock
    /// </summary>
    public (bool success, OperatorLock? operatorLock, string? error) ExtendLock(string userId, int additionalMinutes)
    {
        lock (_lock)
        {
            if (_currentLock == null || _currentLock.IsExpired)
            {
                return (false, null, "No active lock to extend");
            }

            if (_currentLock.UserId != userId)
            {
                return (false, null, "You do not hold the current lock");
            }

            return ExtendLockInternal(userId, additionalMinutes);
        }
    }

    /// <summary>
    /// Check if user holds the current lock
    /// </summary>
    public bool HasLock(string userId)
    {
        lock (_lock)
        {
            return _currentLock != null &&
                   !_currentLock.IsExpired &&
                   _currentLock.UserId == userId;
        }
    }

    /// <summary>
    /// Check if lock is required for write operations and user has it
    /// </summary>
    public (bool canWrite, string? error) CanUserWrite(string userId, UserRole role)
    {
        lock (_lock)
        {
            // Viewers cannot write
            if (role < UserRole.Operator)
            {
                return (false, "Insufficient permissions. Operator role required.");
            }

            // Check if there's a lock
            if (_currentLock == null || _currentLock.IsExpired)
            {
                // No lock - require user to acquire one first
                return (false, "You must acquire a lock before writing. No active lock exists.");
            }

            // Check if this user holds the lock
            if (_currentLock.UserId != userId)
            {
                return (false, $"Lock is held by {_currentLock.DisplayName}. Wait for release or ask them to release.");
            }

            // Update last activity
            _currentLock.LastActivity = DateTime.UtcNow;
            return (true, null);
        }
    }

    /// <summary>
    /// Update last activity (call on each write operation)
    /// </summary>
    public void UpdateActivity(string userId)
    {
        lock (_lock)
        {
            if (_currentLock?.UserId == userId && !_currentLock.IsExpired)
            {
                _currentLock.LastActivity = DateTime.UtcNow;
            }
        }
    }

    #endregion

    #region Private Methods

    private (bool success, OperatorLock? operatorLock, string? error)
        ExtendLockInternal(string userId, int additionalMinutes)
    {
        if (_currentLock == null || _currentLock.UserId != userId)
        {
            return (false, null, "You do not hold the current lock");
        }

        // Calculate new expiry time
        var maxDuration = _settings.MaxLockDurationMinutes;
        var totalDuration = (DateTime.UtcNow.AddMinutes(additionalMinutes) - _currentLock.AcquiredAt).TotalMinutes;

        if (totalDuration > maxDuration)
        {
            // Cap at max duration from original acquisition
            _currentLock.ExpiresAt = _currentLock.AcquiredAt.AddMinutes(maxDuration);
        }
        else
        {
            _currentLock.ExpiresAt = DateTime.UtcNow.AddMinutes(additionalMinutes);
        }

        _currentLock.LastActivity = DateTime.UtcNow;

        Logger.Information("Lock extended for {Username} (expires at {ExpiresAt})",
            _currentLock.Username, _currentLock.ExpiresAt);

        // Fire event
        LockExtended?.Invoke(this, new LockEventArgs
        {
            Lock = _currentLock,
            Reason = "extended"
        });

        return (true, _currentLock, null);
    }

    private void ReleaseLockInternal(string reason)
    {
        if (_currentLock == null) return;

        var releasedLock = _currentLock;
        _currentLock = null;

        Logger.Information("Lock released ({Reason}). Previous holder: {Username}",
            reason, releasedLock.Username);

        // Fire event
        LockReleased?.Invoke(this, new LockEventArgs
        {
            Lock = releasedLock,
            Reason = reason
        });
    }

    private void StartTimeoutChecker()
    {
        // Check for expired locks every 30 seconds
        _timeoutTimer = new Timer(_ =>
        {
            lock (_lock)
            {
                if (_currentLock?.IsExpired == true)
                {
                    ReleaseLockInternal("timeout");
                }
            }
        }, null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    #endregion
}

/// <summary>
/// Lock event arguments
/// </summary>
public class LockEventArgs : EventArgs
{
    public OperatorLock Lock { get; set; } = null!;
    public string Reason { get; set; } = string.Empty; // acquired, released, extended, timeout, forced
}
