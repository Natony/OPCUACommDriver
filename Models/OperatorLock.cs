namespace OpcUaCommunicationEngine.Models;

/// <summary>
/// Represents the current operator lock state
/// Only one operator can hold the lock at a time
/// </summary>
public class OperatorLock
{
    /// <summary>
    /// Unique lock identifier
    /// </summary>
    public string LockId { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// User ID of the lock holder
    /// </summary>
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Username of the lock holder
    /// </summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// Display name of the lock holder
    /// </summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// When the lock was acquired
    /// </summary>
    public DateTime AcquiredAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the lock will expire (auto-release)
    /// </summary>
    public DateTime ExpiresAt { get; set; }

    /// <summary>
    /// Last activity time (updated on each write operation)
    /// Used for idle timeout
    /// </summary>
    public DateTime LastActivity { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Check if the lock has expired
    /// </summary>
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;

    /// <summary>
    /// Time remaining until expiration
    /// </summary>
    public TimeSpan TimeRemaining => IsExpired ? TimeSpan.Zero : ExpiresAt - DateTime.UtcNow;
}

/// <summary>
/// Lock status response DTO
/// </summary>
public class LockStatus
{
    public bool IsLocked { get; set; }
    public string? LockId { get; set; }
    public string? UserId { get; set; }
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public DateTime? AcquiredAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int? TimeRemainingSeconds { get; set; }
}
