namespace OpcUaCommunicationEngine.Models;

/// <summary>
/// Authentication and authorization settings
/// </summary>
public class AuthSettings
{
    /// <summary>
    /// JWT secret key for signing tokens (min 32 characters)
    /// </summary>
    public string JwtSecretKey { get; set; } = "OpcUaCommunicationEngine_SuperSecretKey_2024_ChangeThisInProduction!";

    /// <summary>
    /// JWT issuer
    /// </summary>
    public string JwtIssuer { get; set; } = "OpcUaCommunicationEngine";

    /// <summary>
    /// JWT audience
    /// </summary>
    public string JwtAudience { get; set; } = "OpcUaCommunicationEngine.Clients";

    /// <summary>
    /// Access token expiry in minutes (default: 15 minutes)
    /// </summary>
    public int AccessTokenExpiryMinutes { get; set; } = 15;

    /// <summary>
    /// Refresh token expiry in days (default: 7 days)
    /// </summary>
    public int RefreshTokenExpiryDays { get; set; } = 7;

    /// <summary>
    /// Operator lock timeout in minutes (default: 30 minutes)
    /// Lock will auto-release after this duration of inactivity
    /// </summary>
    public int LockTimeoutMinutes { get; set; } = 30;

    /// <summary>
    /// Maximum lock duration in minutes (default: 120 minutes / 2 hours)
    /// Even with activity, lock will expire after this duration
    /// </summary>
    public int MaxLockDurationMinutes { get; set; } = 120;

    /// <summary>
    /// Path to users storage file
    /// </summary>
    public string UsersFilePath { get; set; } = "Configurations/users.json";

    /// <summary>
    /// Enable authentication (set to false for development/testing)
    /// </summary>
    public bool EnableAuthentication { get; set; } = true;
}
