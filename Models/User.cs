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
}
