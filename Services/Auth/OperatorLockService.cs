using System.IO;
using Newtonsoft.Json;
using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Models;
using Serilog;

namespace OpcUaCommunicationEngine.Services.Auth;

/// <summary>
/// Service for operator lock management
/// Ensures only one operator can control the system at a time
/// </summary>
public class OperatorLockService : IDisposable
{
    private bool _disposed = false;
    private readonly AuthSettings _settings;
    private readonly object _lock = new();
    private readonly string _lockStatePath = "Configurations/lock_state.json";
    private OperatorLock? _currentLock;
    private Timer? _timeoutTimer;
    private FileSystemWatcher? _fileWatcher;
    private bool _isInternalUpdate = false; // Flag to prevent self-triggered events
    private DateTime _lastFileChange = DateTime.MinValue;
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
        LoadLockState(); // Restore lock state from file
        StartTimeoutChecker();
        StartFileWatcher(); // Watch for external changes to lock file
    }

    #region Public Methods

    /// <summary>
    /// Get current lock status
    /// </summary>
    public LockStatus GetLockStatus()
    {
        lock (_lock)
        {
            if (_currentLock == null)
            {
                return new LockStatus { IsLocked = false };
            }

            // Debug logging
            Logger.Debug("GetLockStatus: ExpiresAt={ExpiresAt}, UtcNow={UtcNow}, IsExpired={IsExpired}, TimeRemaining={TimeRemaining}",
                _currentLock.ExpiresAt, DateTime.UtcNow, _currentLock.IsExpired, _currentLock.TimeRemaining);

            if (_currentLock.IsExpired)
            {
                Logger.Information("Lock expired during status check");
                ReleaseLockInternal("timeout");
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
                    var result = ExtendLockInternal(userId, durationMinutes ?? _settings.LockTimeoutMinutes);
                    return (result.success, result.operatorLock, result.error, null, null);
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
            // Admin: unlimited (DateTime.MaxValue)
            // durationMinutes = 0: unlimited (configured per user)
            // Others: configurable with max limit
            DateTime expiresAt;
            string durationText;

            if (role == UserRole.Admin || durationMinutes == 0)
            {
                // Admin lock or user configured as unlimited - never expires automatically
                expiresAt = DateTime.MaxValue;
                durationText = "unlimited";
            }
            else
            {
                var duration = durationMinutes ?? _settings.LockTimeoutMinutes;
                var maxDuration = _settings.MaxLockDurationMinutes;
                duration = Math.Min(duration, maxDuration);
                expiresAt = DateTime.UtcNow.AddMinutes(duration);
                durationText = $"{duration} minutes";
            }

            // Acquire new lock
            _currentLock = new OperatorLock
            {
                LockId = Guid.NewGuid().ToString(),
                UserId = userId,
                Username = username,
                DisplayName = displayName,
                Role = role,
                AcquiredAt = DateTime.UtcNow,
                ExpiresAt = expiresAt,
                LastActivity = DateTime.UtcNow
            };

            // Save lock state to file
            SaveLockState();

            Logger.Information("Lock acquired by {Username} (Role: {Role}, expires in {Duration})",
                username, role, durationText);

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

        // Save lock state to file
        SaveLockState();

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

        // Save lock state to file (removes the file)
        SaveLockState();

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

    private void StartFileWatcher()
    {
        try
        {
            var directory = Path.GetDirectoryName(_lockStatePath);
            if (string.IsNullOrEmpty(directory))
            {
                directory = ".";
            }

            // Ensure directory exists
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            var fileName = Path.GetFileName(_lockStatePath);

            _fileWatcher = new FileSystemWatcher(directory, fileName)
            {
                NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.CreationTime | NotifyFilters.FileName,
                EnableRaisingEvents = true
            };

            _fileWatcher.Changed += OnLockFileChanged;
            _fileWatcher.Created += OnLockFileChanged;
            _fileWatcher.Deleted += OnLockFileDeleted;

            Logger.Debug("FileSystemWatcher started for lock state file: {Path}", _lockStatePath);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to start FileSystemWatcher for lock state file");
        }
    }

    private void OnLockFileChanged(object sender, FileSystemEventArgs e)
    {
        // Ignore if this is our own update
        if (_isInternalUpdate)
        {
            return;
        }

        // Debounce rapid file changes (FileSystemWatcher may fire multiple events)
        var now = DateTime.UtcNow;
        if ((now - _lastFileChange).TotalMilliseconds < 500)
        {
            return;
        }
        _lastFileChange = now;

        Logger.Debug("Lock file changed externally: {ChangeType}", e.ChangeType);

        // Small delay to ensure file is fully written
        Task.Delay(100).ContinueWith(_ => ReloadLockStateFromFile());
    }

    private void OnLockFileDeleted(object sender, FileSystemEventArgs e)
    {
        // Ignore if this is our own update
        if (_isInternalUpdate)
        {
            return;
        }

        Logger.Debug("Lock file deleted externally");

        lock (_lock)
        {
            if (_currentLock != null)
            {
                var releasedLock = _currentLock;
                _currentLock = null;

                Logger.Information("Lock released externally. Previous holder: {Username}", releasedLock.Username);

                // Fire event to notify UI
                LockReleased?.Invoke(this, new LockEventArgs
                {
                    Lock = releasedLock,
                    Reason = "external"
                });
            }
        }
    }

    private void ReloadLockStateFromFile()
    {
        try
        {
            if (!File.Exists(_lockStatePath))
            {
                // File deleted - lock released
                lock (_lock)
                {
                    if (_currentLock != null)
                    {
                        var releasedLock = _currentLock;
                        _currentLock = null;

                        Logger.Information("Lock released (file not found). Previous holder: {Username}", releasedLock.Username);

                        LockReleased?.Invoke(this, new LockEventArgs
                        {
                            Lock = releasedLock,
                            Reason = "external"
                        });
                    }
                }
                return;
            }

            var json = File.ReadAllText(_lockStatePath);
            var loadedLock = JsonConvert.DeserializeObject<OperatorLock>(json);

            if (loadedLock == null)
            {
                Logger.Warning("Failed to deserialize external lock state");
                return;
            }

            lock (_lock)
            {
                var previousLock = _currentLock;

                // Check if lock has changed
                if (previousLock == null && loadedLock != null && !loadedLock.IsExpired)
                {
                    // New lock acquired externally
                    _currentLock = loadedLock;
                    Logger.Information("Lock acquired externally by {Username}", loadedLock.Username);

                    LockAcquired?.Invoke(this, new LockEventArgs
                    {
                        Lock = loadedLock,
                        Reason = "external"
                    });
                }
                else if (previousLock != null && loadedLock != null && !loadedLock.IsExpired)
                {
                    // Lock changed or extended
                    if (previousLock.LockId != loadedLock.LockId)
                    {
                        // Different lock - new user acquired it
                        _currentLock = loadedLock;
                        Logger.Information("Lock taken over by {Username} (previous: {PreviousUsername})",
                            loadedLock.Username, previousLock.Username);

                        // Fire released for old, acquired for new
                        LockReleased?.Invoke(this, new LockEventArgs
                        {
                            Lock = previousLock,
                            Reason = "external_takeover"
                        });

                        LockAcquired?.Invoke(this, new LockEventArgs
                        {
                            Lock = loadedLock,
                            Reason = "external"
                        });
                    }
                    else if (previousLock.ExpiresAt != loadedLock.ExpiresAt)
                    {
                        // Same lock but extended
                        _currentLock = loadedLock;
                        Logger.Information("Lock extended externally for {Username}", loadedLock.Username);

                        LockExtended?.Invoke(this, new LockEventArgs
                        {
                            Lock = loadedLock,
                            Reason = "external"
                        });
                    }
                }
                else if (loadedLock?.IsExpired == true)
                {
                    // Loaded lock is expired
                    if (previousLock != null)
                    {
                        _currentLock = null;
                        Logger.Information("Lock expired (detected from file). Previous holder: {Username}", previousLock.Username);

                        LockReleased?.Invoke(this, new LockEventArgs
                        {
                            Lock = previousLock,
                            Reason = "timeout"
                        });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to reload lock state from file");
        }
    }

    private void SaveLockState()
    {
        try
        {
            // Set flag to prevent FileSystemWatcher from triggering on our own changes
            _isInternalUpdate = true;

            var directory = Path.GetDirectoryName(_lockStatePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (_currentLock == null)
            {
                // No lock - delete the file if it exists
                if (File.Exists(_lockStatePath))
                {
                    File.Delete(_lockStatePath);
                }
            }
            else
            {
                // Save current lock state using atomic write (temp file + rename)
                var tempPath = _lockStatePath + ".tmp";
                var json = JsonConvert.SerializeObject(_currentLock, Formatting.Indented);
                File.WriteAllText(tempPath, json);

                // Atomic replace (on most file systems)
                if (File.Exists(_lockStatePath))
                {
                    File.Delete(_lockStatePath);
                }
                File.Move(tempPath, _lockStatePath);

                Logger.Debug("Lock state saved to {Path}", _lockStatePath);
            }
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to save lock state to {Path}", _lockStatePath);
        }
        finally
        {
            // Reset flag after a short delay to ensure FileSystemWatcher events are processed
            Task.Delay(200).ContinueWith(_ => _isInternalUpdate = false);
        }
    }

    private void LoadLockState()
    {
        try
        {
            // Clean up any temp files from interrupted writes
            var tempPath = _lockStatePath + ".tmp";
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
                Logger.Debug("Cleaned up temp lock state file");
            }

            if (!File.Exists(_lockStatePath))
            {
                Logger.Debug("No lock state file found at {Path}", _lockStatePath);
                return;
            }

            var json = File.ReadAllText(_lockStatePath);
            var loadedLock = JsonConvert.DeserializeObject<OperatorLock>(json);

            if (loadedLock == null)
            {
                Logger.Warning("Failed to deserialize lock state from {Path}, removing corrupted file", _lockStatePath);
                File.Delete(_lockStatePath);
                return;
            }

            // Check if the loaded lock is still valid (not expired)
            if (loadedLock.IsExpired)
            {
                Logger.Information("Loaded lock has expired, discarding. Previous holder: {Username}", loadedLock.Username);
                File.Delete(_lockStatePath);
                return;
            }

            // Restore the lock
            _currentLock = loadedLock;
            Logger.Information("Lock state restored from file. Lock held by {Username} (Role: {Role}, expires at {ExpiresAt})",
                _currentLock.Username, _currentLock.Role, _currentLock.ExpiresAt);
        }
        catch (Exception ex)
        {
            Logger.Error(ex, "Failed to load lock state from {Path}, removing corrupted file", _lockStatePath);
            // Clean up corrupted file
            try
            {
                if (File.Exists(_lockStatePath))
                {
                    File.Delete(_lockStatePath);
                }
            }
            catch { /* Ignore cleanup errors */ }
        }
    }

    #endregion

    #region IDisposable

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;

        if (disposing)
        {
            // Dispose managed resources
            _timeoutTimer?.Dispose();
            _timeoutTimer = null;

            if (_fileWatcher != null)
            {
                _fileWatcher.EnableRaisingEvents = false;
                _fileWatcher.Changed -= OnLockFileChanged;
                _fileWatcher.Created -= OnLockFileChanged;
                _fileWatcher.Deleted -= OnLockFileDeleted;
                _fileWatcher.Dispose();
                _fileWatcher = null;
            }

            Logger.Debug("OperatorLockService disposed");
        }

        _disposed = true;
    }

    ~OperatorLockService()
    {
        Dispose(false);
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
