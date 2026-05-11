using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using OpcUaCommunicationEngine.Models;
using Serilog;

namespace OpcUaCommunicationEngine.Services.Auth;

/// <summary>
/// Service for JWT authentication
/// </summary>
public class AuthService
{
    private readonly AuthSettings _settings;
    private readonly UserService _userService;
    private readonly Dictionary<string, RefreshToken> _refreshTokens = new();
    // Active sessions: SessionId -> ActiveSession
    private readonly Dictionary<string, ActiveSession> _activeSessions = new();
    // User to current session mapping: UserId -> SessionId (for quick lookup)
    private readonly Dictionary<string, string> _userCurrentSession = new();
    private readonly object _lock = new();
    private static ILogger Logger => Log.Logger;

    public AuthService(AuthSettings settings, UserService userService)
    {
        _settings = settings;
        _userService = userService;
    }

    #region Public Methods

    /// <summary>
    /// Authenticate user and generate tokens (legacy method for backward compatibility)
    /// </summary>
    public (string? accessToken, string? refreshToken, DateTime? expiresAt, User? user) Login(string username, string password)
    {
        var result = LoginWithDevice(username, password, null, null);
        return (result.accessToken, result.refreshToken, result.expiresAt, result.user);
    }

    /// <summary>
    /// Authenticate user and generate tokens with device tracking for single-session enforcement
    /// </summary>
    public (string? accessToken, string? refreshToken, DateTime? expiresAt, User? user, string? sessionId, bool previousSessionTerminated, string? previousDeviceName) LoginWithDevice(
        string username, string password, string? deviceId, string? deviceName)
    {
        var user = _userService.ValidateCredentials(username, password);
        if (user == null)
        {
            Logger.Warning("Failed login attempt for user: {Username}", username);
            return (null, null, null, null, null, false, null);
        }

        bool previousSessionTerminated = false;
        string? previousDeviceName = null;

        lock (_lock)
        {
            // Check if user has an existing active session
            if (_userCurrentSession.TryGetValue(user.Id, out var existingSessionId) &&
                _activeSessions.TryGetValue(existingSessionId, out var existingSession) &&
                existingSession.IsActive)
            {
                // Invalidate the previous session
                existingSession.IsActive = false;
                existingSession.InvalidatedAt = DateTime.UtcNow;
                existingSession.InvalidReason = "LOGGED_IN_FROM_ANOTHER_DEVICE";
                existingSession.InvalidatedByDeviceName = deviceName ?? deviceId ?? "Unknown Device";

                previousSessionTerminated = true;
                previousDeviceName = existingSession.DeviceName ?? existingSession.DeviceId;

                Logger.Information("User {Username} session terminated from device {OldDevice} due to login from {NewDevice}",
                    username, previousDeviceName, deviceName ?? deviceId ?? "Unknown");

                // Revoke all refresh tokens for this user
                var tokensToRevoke = _refreshTokens
                    .Where(kvp => kvp.Value.UserId == user.Id && !kvp.Value.IsRevoked)
                    .ToList();
                foreach (var kvp in tokensToRevoke)
                {
                    kvp.Value.IsRevoked = true;
                }
            }

            // Create new session
            var session = new ActiveSession
            {
                SessionId = Guid.NewGuid().ToString(),
                UserId = user.Id,
                Username = user.Username,
                DeviceId = deviceId ?? Guid.NewGuid().ToString(),
                DeviceName = deviceName,
                LoginAt = DateTime.UtcNow,
                LastActivityAt = DateTime.UtcNow,
                IsActive = true
            };

            _activeSessions[session.SessionId] = session;
            _userCurrentSession[user.Id] = session.SessionId;

            var accessToken = GenerateAccessToken(user);
            var refreshToken = GenerateRefreshToken(user.Id, session.SessionId);
            var expiresAt = DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpiryMinutes);

            Logger.Information("User logged in: {Username} from device: {DeviceName} ({DeviceId})",
                username, deviceName ?? "Unknown", deviceId ?? "Unknown");

            return (accessToken, refreshToken, expiresAt, user, session.SessionId, previousSessionTerminated, previousDeviceName);
        }
    }

    /// <summary>
    /// Refresh access token using refresh token
    /// </summary>
    public (string? accessToken, string? refreshToken, DateTime? expiresAt, User? user) RefreshToken(string refreshToken)
    {
        lock (_lock)
        {
            if (!_refreshTokens.TryGetValue(refreshToken, out var token))
            {
                return (null, null, null, null);
            }

            if (token.IsRevoked || token.ExpiresAt < DateTime.UtcNow)
            {
                _refreshTokens.Remove(refreshToken);
                return (null, null, null, null);
            }

            var user = _userService.GetUserById(token.UserId);
            if (user == null || !user.IsActive)
            {
                _refreshTokens.Remove(refreshToken);
                return (null, null, null, null);
            }

            // Revoke old refresh token
            token.IsRevoked = true;

            // Generate new tokens
            var newAccessToken = GenerateAccessToken(user);
            var newRefreshToken = GenerateRefreshToken(user.Id);
            var expiresAt = DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpiryMinutes);

            Logger.Debug("Tokens refreshed for user: {Username}", user.Username);
            return (newAccessToken, newRefreshToken, expiresAt, user);
        }
    }

    /// <summary>
    /// Logout - revoke refresh token
    /// </summary>
    public void Logout(string refreshToken)
    {
        lock (_lock)
        {
            if (_refreshTokens.TryGetValue(refreshToken, out var token))
            {
                token.IsRevoked = true;
                _refreshTokens.Remove(refreshToken);
            }
        }
    }

    /// <summary>
    /// Logout user by ID - revoke all their refresh tokens
    /// </summary>
    public void LogoutUser(string userId)
    {
        lock (_lock)
        {
            var tokensToRemove = _refreshTokens
                .Where(kvp => kvp.Value.UserId == userId)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var token in tokensToRemove)
            {
                _refreshTokens.Remove(token);
            }
        }
    }

    /// <summary>
    /// Validate access token and return claims principal
    /// </summary>
    public ClaimsPrincipal? ValidateToken(string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_settings.JwtSecretKey);

            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = true,
                ValidIssuer = _settings.JwtIssuer,
                ValidateAudience = true,
                ValidAudience = _settings.JwtAudience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            };

            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            return principal;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get token validation parameters for ASP.NET Core middleware
    /// </summary>
    public TokenValidationParameters GetTokenValidationParameters()
    {
        var key = Encoding.UTF8.GetBytes(_settings.JwtSecretKey);

        return new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(key),
            ValidateIssuer = true,
            ValidIssuer = _settings.JwtIssuer,
            ValidateAudience = true,
            ValidAudience = _settings.JwtAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
            // Return user ID from claims
            NameClaimType = ClaimTypes.NameIdentifier,
            RoleClaimType = ClaimTypes.Role
        };
    }

    /// <summary>
    /// Clean up expired refresh tokens
    /// </summary>
    public void CleanupExpiredTokens()
    {
        lock (_lock)
        {
            var expiredTokens = _refreshTokens
                .Where(kvp => kvp.Value.ExpiresAt < DateTime.UtcNow || kvp.Value.IsRevoked)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var token in expiredTokens)
            {
                _refreshTokens.Remove(token);
            }

            if (expiredTokens.Any())
            {
                Logger.Debug("Cleaned up {Count} expired refresh tokens", expiredTokens.Count);
            }
        }
    }

    #region Session Management

    /// <summary>
    /// Validate if a session is still active
    /// </summary>
    public (bool isValid, string? invalidReason, string? newDeviceName, DateTime? invalidatedAt) ValidateSession(string sessionId, string deviceId)
    {
        lock (_lock)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
            {
                return (false, "SESSION_NOT_FOUND", null, null);
            }

            if (!session.IsActive)
            {
                return (false, session.InvalidReason ?? "SESSION_INVALIDATED", session.InvalidatedByDeviceName, session.InvalidatedAt);
            }

            // Verify device ID matches
            if (session.DeviceId != deviceId)
            {
                return (false, "DEVICE_MISMATCH", null, null);
            }

            return (true, null, null, null);
        }
    }

    /// <summary>
    /// Update session last activity (heartbeat)
    /// </summary>
    public (bool success, string? invalidReason) Heartbeat(string sessionId, string deviceId)
    {
        lock (_lock)
        {
            if (!_activeSessions.TryGetValue(sessionId, out var session))
            {
                return (false, "SESSION_NOT_FOUND");
            }

            if (!session.IsActive)
            {
                return (false, session.InvalidReason ?? "SESSION_INVALIDATED");
            }

            if (session.DeviceId != deviceId)
            {
                return (false, "DEVICE_MISMATCH");
            }

            session.LastActivityAt = DateTime.UtcNow;
            return (true, null);
        }
    }

    /// <summary>
    /// Force logout a user and invalidate their session
    /// </summary>
    public void ForceLogoutUser(string userId, string? reason = null)
    {
        lock (_lock)
        {
            // Invalidate active session
            if (_userCurrentSession.TryGetValue(userId, out var sessionId) &&
                _activeSessions.TryGetValue(sessionId, out var session))
            {
                session.IsActive = false;
                session.InvalidatedAt = DateTime.UtcNow;
                session.InvalidReason = reason ?? "FORCED_LOGOUT_BY_ADMIN";
            }

            // Revoke all refresh tokens
            var tokensToRevoke = _refreshTokens
                .Where(kvp => kvp.Value.UserId == userId)
                .ToList();
            foreach (var kvp in tokensToRevoke)
            {
                kvp.Value.IsRevoked = true;
                _refreshTokens.Remove(kvp.Key);
            }

            var user = _userService.GetUserById(userId);
            Logger.Information("User {Username} was force logged out. Reason: {Reason}",
                user?.Username ?? userId, reason ?? "Admin action");
        }
    }

    /// <summary>
    /// Logout a specific session
    /// </summary>
    public void LogoutSession(string sessionId)
    {
        lock (_lock)
        {
            if (_activeSessions.TryGetValue(sessionId, out var session))
            {
                session.IsActive = false;
                session.InvalidatedAt = DateTime.UtcNow;
                session.InvalidReason = "USER_LOGGED_OUT";

                // Revoke associated refresh tokens
                var tokensToRevoke = _refreshTokens
                    .Where(kvp => kvp.Value.SessionId == sessionId)
                    .ToList();
                foreach (var kvp in tokensToRevoke)
                {
                    kvp.Value.IsRevoked = true;
                    _refreshTokens.Remove(kvp.Key);
                }

                Logger.Information("Session {SessionId} logged out for user {Username}",
                    sessionId, session.Username);
            }
        }
    }

    /// <summary>
    /// Get all active sessions (admin function)
    /// </summary>
    public List<ActiveSession> GetAllActiveSessions()
    {
        lock (_lock)
        {
            return _activeSessions.Values
                .Where(s => s.IsActive)
                .OrderByDescending(s => s.LoginAt)
                .ToList();
        }
    }

    /// <summary>
    /// Get session by user ID
    /// </summary>
    public ActiveSession? GetUserSession(string userId)
    {
        lock (_lock)
        {
            if (_userCurrentSession.TryGetValue(userId, out var sessionId) &&
                _activeSessions.TryGetValue(sessionId, out var session) &&
                session.IsActive)
            {
                return session;
            }
            return null;
        }
    }

    /// <summary>
    /// Clean up inactive sessions (older than specified days)
    /// </summary>
    public void CleanupInactiveSessions(int olderThanDays = 7)
    {
        lock (_lock)
        {
            var cutoff = DateTime.UtcNow.AddDays(-olderThanDays);
            var sessionsToRemove = _activeSessions
                .Where(kvp => !kvp.Value.IsActive && kvp.Value.InvalidatedAt < cutoff)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var sessionId in sessionsToRemove)
            {
                _activeSessions.Remove(sessionId);
            }

            if (sessionsToRemove.Any())
            {
                Logger.Debug("Cleaned up {Count} inactive sessions", sessionsToRemove.Count);
            }
        }
    }

    #endregion

    #endregion

    #region Private Methods

    private string GenerateAccessToken(User user)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var key = Encoding.UTF8.GetBytes(_settings.JwtSecretKey);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id),
            new(ClaimTypes.Name, user.Username),
            new("displayName", user.DisplayName),
            new(ClaimTypes.Role, user.Role.ToString()),
            new("roleLevel", ((int)user.Role).ToString()),
            new("lockDurationMinutes", user.LockDurationMinutes?.ToString() ?? "")
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpiryMinutes),
            Issuer = _settings.JwtIssuer,
            Audience = _settings.JwtAudience,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(key),
                SecurityAlgorithms.HmacSha256Signature)
        };

        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    private string GenerateRefreshToken(string userId, string? sessionId = null)
    {
        lock (_lock)
        {
            var randomBytes = new byte[64];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(randomBytes);
            }

            var token = Convert.ToBase64String(randomBytes);

            _refreshTokens[token] = new RefreshToken
            {
                Token = token,
                UserId = userId,
                ExpiresAt = DateTime.UtcNow.AddDays(_settings.RefreshTokenExpiryDays),
                CreatedAt = DateTime.UtcNow,
                SessionId = sessionId
            };

            return token;
        }
    }

    #endregion
}
