using OpcUaCommunicationEngine.Enums;

namespace OpcUaCommunicationEngine.Models;

/// <summary>
/// User account model
/// </summary>
public class User
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Viewer;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    /// <summary>
    /// Custom lock duration in minutes for this user.
    /// null = use default from AuthSettings
    /// 0 = unlimited (same as admin)
    /// </summary>
    public int? LockDurationMinutes { get; set; }
}

/// <summary>
/// User data storage
/// </summary>
public class UserStorage
{
    public List<User> Users { get; set; } = new();
}

/// <summary>
/// Refresh token for JWT authentication
/// </summary>
public class RefreshToken
{
    public string Token { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsRevoked { get; set; } = false;
    /// <summary>
    /// Associated session ID for this refresh token
    /// </summary>
    public string? SessionId { get; set; }
}

/// <summary>
/// Active session tracking for single-device login enforcement
/// </summary>
public class ActiveSession
{
    public string SessionId { get; set; } = Guid.NewGuid().ToString();
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string? DeviceName { get; set; }
    public DateTime LoginAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    /// <summary>
    /// When this session was invalidated (if invalidated by another login)
    /// </summary>
    public DateTime? InvalidatedAt { get; set; }
    /// <summary>
    /// Reason for invalidation
    /// </summary>
    public string? InvalidReason { get; set; }
    /// <summary>
    /// Device name that caused this session to be invalidated
    /// </summary>
    public string? InvalidatedByDeviceName { get; set; }
}
