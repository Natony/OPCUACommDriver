using OpcUaCommunicationEngine.Enums;

namespace OpcUaCommunicationEngine.Api.Models;

#region Authentication DTOs

/// <summary>
/// Login request
/// </summary>
public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    /// <summary>
    /// Unique identifier for the client machine/device.
    /// Used to enforce single-session per user (one device at a time).
    /// </summary>
    public string? DeviceId { get; set; }
    /// <summary>
    /// Optional device name for display purposes
    /// </summary>
    public string? DeviceName { get; set; }
}

/// <summary>
/// Login response
/// </summary>
public class LoginResponse
{
    public bool Success { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public UserDto? User { get; set; }
    public string? Error { get; set; }
    /// <summary>
    /// Session ID for the current login session
    /// </summary>
    public string? SessionId { get; set; }
    /// <summary>
    /// Indicates if another device was logged out due to this login
    /// </summary>
    public bool PreviousSessionTerminated { get; set; }
    /// <summary>
    /// Information about the previous session that was terminated (if any)
    /// </summary>
    public string? PreviousDeviceName { get; set; }
}

/// <summary>
/// Refresh token request
/// </summary>
public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}

/// <summary>
/// User DTO (without password)
/// </summary>
public class UserDto
{
    public string Id { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; }
    public string RoleName => Role.ToString();
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
}

#endregion

#region User Management DTOs

/// <summary>
/// Create user request
/// </summary>
public class CreateUserRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Viewer;
}

/// <summary>
/// Update user request
/// </summary>
public class UpdateUserRequest
{
    public string? DisplayName { get; set; }
    public UserRole? Role { get; set; }
    public bool? IsActive { get; set; }
}

/// <summary>
/// Reset password request
/// </summary>
public class ResetPasswordRequest
{
    public string NewPassword { get; set; } = string.Empty;
}

/// <summary>
/// Change own password request
/// </summary>
public class ChangePasswordRequest
{
    public string CurrentPassword { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
}

#endregion

#region Lock Management DTOs

/// <summary>
/// Acquire lock request
/// </summary>
public class AcquireLockRequest
{
    /// <summary>
    /// Lock duration in minutes (optional, uses default if not specified)
    /// </summary>
    public int? DurationMinutes { get; set; }
}

/// <summary>
/// Acquire lock response
/// </summary>
public class AcquireLockResponse
{
    public bool Success { get; set; }
    public string? LockId { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int? TimeRemainingSeconds { get; set; }
    public string? Error { get; set; }
    public string? LockedByUsername { get; set; }
    public string? LockedByDisplayName { get; set; }
}

/// <summary>
/// Extend lock request
/// </summary>
public class ExtendLockRequest
{
    /// <summary>
    /// Additional duration in minutes
    /// </summary>
    public int DurationMinutes { get; set; } = 30;
}

/// <summary>
/// Lock status response
/// </summary>
public class LockStatusResponse
{
    public bool Success { get; set; } = true;
    public bool IsLocked { get; set; }
    public string? LockId { get; set; }
    public string? UserId { get; set; }
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public DateTime? AcquiredAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public int? TimeRemainingSeconds { get; set; }
    public bool IsCurrentUser { get; set; }
}

#endregion

#region Session Management DTOs

/// <summary>
/// Request to validate if the current session is still active
/// </summary>
public class ValidateSessionRequest
{
    /// <summary>
    /// The session ID received during login
    /// </summary>
    public string SessionId { get; set; } = string.Empty;
    /// <summary>
    /// The device ID of the client
    /// </summary>
    public string DeviceId { get; set; } = string.Empty;
}

/// <summary>
/// Response for session validation
/// </summary>
public class ValidateSessionResponse
{
    public bool Success { get; set; }
    /// <summary>
    /// True if the session is still valid and active
    /// </summary>
    public bool IsValid { get; set; }
    /// <summary>
    /// Error message if session is invalid
    /// </summary>
    public string? Error { get; set; }
    /// <summary>
    /// Reason why session was invalidated (e.g., "LOGGED_IN_FROM_ANOTHER_DEVICE")
    /// </summary>
    public string? InvalidReason { get; set; }
    /// <summary>
    /// Information about the device that caused this session to be invalidated
    /// </summary>
    public string? NewDeviceName { get; set; }
    /// <summary>
    /// When the session was invalidated
    /// </summary>
    public DateTime? InvalidatedAt { get; set; }
}

/// <summary>
/// Active session information
/// </summary>
public class SessionInfo
{
    public string SessionId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string? DeviceName { get; set; }
    public DateTime LoginAt { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public bool IsActive { get; set; }
}

/// <summary>
/// Response for getting all active sessions (admin only)
/// </summary>
public class ActiveSessionsResponse
{
    public bool Success { get; set; }
    public List<SessionInfo> Sessions { get; set; } = new();
    public string? Error { get; set; }
}

/// <summary>
/// Request to force logout a user from all devices
/// </summary>
public class ForceLogoutRequest
{
    /// <summary>
    /// User ID to force logout
    /// </summary>
    public string UserId { get; set; } = string.Empty;
    /// <summary>
    /// Reason for force logout (will be shown to the user)
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// Heartbeat request to keep session alive
/// </summary>
public class HeartbeatRequest
{
    public string SessionId { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
}

/// <summary>
/// Heartbeat response
/// </summary>
public class HeartbeatResponse
{
    public bool Success { get; set; }
    /// <summary>
    /// False if session has been invalidated (user should logout)
    /// </summary>
    public bool SessionValid { get; set; }
    public string? Error { get; set; }
    /// <summary>
    /// Reason if session is no longer valid
    /// </summary>
    public string? InvalidReason { get; set; }
}

#endregion
