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
}

/// <summary>
/// Login response
/// </summary>
public class LoginResponse
{
    public bool Success { get; set; }
    public string? Token { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public UserDto? User { get; set; }
    public string? Error { get; set; }
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
