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
    private readonly object _lock = new();
    private static ILogger Logger => Log.Logger;

    public AuthService(AuthSettings settings, UserService userService)
    {
        _settings = settings;
        _userService = userService;
    }

    #region Public Methods

    /// <summary>
    /// Authenticate user and generate tokens
    /// </summary>
    public (string? accessToken, string? refreshToken, DateTime? expiresAt, User? user) Login(string username, string password)
    {
        var user = _userService.ValidateCredentials(username, password);
        if (user == null)
        {
            Logger.Warning("Failed login attempt for user: {Username}", username);
            return (null, null, null, null);
        }

        var accessToken = GenerateAccessToken(user);
        var refreshToken = GenerateRefreshToken(user.Id);
        var expiresAt = DateTime.UtcNow.AddMinutes(_settings.AccessTokenExpiryMinutes);

        Logger.Information("User logged in: {Username}", username);
        return (accessToken, refreshToken, expiresAt, user);
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

    private string GenerateRefreshToken(string userId)
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
                CreatedAt = DateTime.UtcNow
            };

            return token;
        }
    }

    #endregion
}
