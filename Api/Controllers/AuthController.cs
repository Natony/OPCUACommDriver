using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpcUaCommunicationEngine.Api.Models;
using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Helpers;
using OpcUaCommunicationEngine.Services.Auth;
using Serilog;

namespace OpcUaCommunicationEngine.Api.Controllers;

/// <summary>
/// Authentication controller
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly UserService _userService;
    private static ILogger Logger => Log.Logger;

    public AuthController(AuthService authService, UserService userService)
    {
        _authService = authService;
        _userService = userService;
    }

    /// <summary>
    /// Login with username and password.
    /// Supports single-session per user - if the user is already logged in from another device,
    /// the previous session will be terminated.
    /// </summary>
    /// <remarks>
    /// **Important**: Include DeviceId in the request to enable single-device login enforcement.
    /// When a user logs in from a new device, any existing session on other devices will be invalidated.
    /// The client should check the `PreviousSessionTerminated` flag in the response.
    /// </remarks>
    [HttpPost("login")]
    [AllowAnonymous]
    public ActionResult<LoginResponse> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new LoginResponse
            {
                Success = false,
                Error = "Username and password are required"
            });
        }

        var (accessToken, refreshToken, expiresAt, user, sessionId, previousSessionTerminated, previousDeviceName) =
            _authService.LoginWithDevice(request.Username, request.Password, request.DeviceId, request.DeviceName);

        if (user == null)
        {
            return Unauthorized(new LoginResponse
            {
                Success = false,
                Error = "Invalid username or password"
            });
        }

        return Ok(new LoginResponse
        {
            Success = true,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            SessionId = sessionId,
            PreviousSessionTerminated = previousSessionTerminated,
            PreviousDeviceName = previousDeviceName,
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                DisplayName = user.DisplayName,
                Role = user.Role,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt
            }
        });
    }

    /// <summary>
    /// Refresh access token
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    public ActionResult<LoginResponse> RefreshToken([FromBody] RefreshTokenRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return BadRequest(new LoginResponse
            {
                Success = false,
                Error = "Refresh token is required"
            });
        }

        var (accessToken, refreshToken, expiresAt, user) = _authService.RefreshToken(request.RefreshToken);

        if (user == null)
        {
            return Unauthorized(new LoginResponse
            {
                Success = false,
                Error = "Invalid or expired refresh token"
            });
        }

        return Ok(new LoginResponse
        {
            Success = true,
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            ExpiresAt = expiresAt,
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                DisplayName = user.DisplayName,
                Role = user.Role,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt
            }
        });
    }

    /// <summary>
    /// Logout - revoke refresh token
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public ActionResult<ApiResponse> Logout([FromBody] RefreshTokenRequest? request)
    {
        if (!string.IsNullOrWhiteSpace(request?.RefreshToken))
        {
            _authService.Logout(request.RefreshToken);
        }

        return Ok(new ApiResponse
        {
            Success = true,
            Message = "Logged out successfully"
        });
    }

    /// <summary>
    /// Get current user info
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public ActionResult<ApiResponse<UserDto>> GetCurrentUser()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new ApiResponse<UserDto>
            {
                Success = false,
                Error = "User not found"
            });
        }

        var user = _userService.GetUserById(userId);
        if (user == null)
        {
            return NotFound(new ApiResponse<UserDto>
            {
                Success = false,
                Error = "User not found"
            });
        }

        return Ok(new ApiResponse<UserDto>
        {
            Success = true,
            Data = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                DisplayName = user.DisplayName,
                Role = user.Role,
                IsActive = user.IsActive,
                CreatedAt = user.CreatedAt,
                LastLoginAt = user.LastLoginAt
            }
        });
    }

    /// <summary>
    /// Change own password
    /// </summary>
    [HttpPost("change-password")]
    [Authorize]
    public ActionResult<ApiResponse> ChangePassword([FromBody] ChangePasswordRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new ApiResponse { Success = false, Error = "User not found" });
        }

        if (string.IsNullOrWhiteSpace(request.CurrentPassword) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return BadRequest(new ApiResponse
            {
                Success = false,
                Error = "Current password and new password are required"
            });
        }

        var (isValidPassword, passwordError) = PasswordValidator.Validate(request.NewPassword);
        if (!isValidPassword)
        {
            return BadRequest(new ApiResponse
            {
                Success = false,
                Error = passwordError
            });
        }

        var success = _userService.ChangePassword(userId, request.CurrentPassword, request.NewPassword);

        if (!success)
        {
            return BadRequest(new ApiResponse
            {
                Success = false,
                Error = "Current password is incorrect"
            });
        }

        return Ok(new ApiResponse
        {
            Success = true,
            Message = "Password changed successfully"
        });
    }

    #region Session Management APIs

    /// <summary>
    /// Validate if the current session is still active.
    /// Clients should call this periodically or when resuming from background.
    /// If the session is invalid, the client should logout and redirect to login screen.
    /// </summary>
    /// <remarks>
    /// Returns `IsValid: false` with `InvalidReason: "LOGGED_IN_FROM_ANOTHER_DEVICE"`
    /// when the user has logged in from another device.
    /// </remarks>
    [HttpPost("validate-session")]
    [AllowAnonymous]
    public ActionResult<ValidateSessionResponse> ValidateSession([FromBody] ValidateSessionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId) || string.IsNullOrWhiteSpace(request.DeviceId))
        {
            return BadRequest(new ValidateSessionResponse
            {
                Success = false,
                IsValid = false,
                Error = "SessionId and DeviceId are required"
            });
        }

        var (isValid, invalidReason, newDeviceName, invalidatedAt) =
            _authService.ValidateSession(request.SessionId, request.DeviceId);

        return Ok(new ValidateSessionResponse
        {
            Success = true,
            IsValid = isValid,
            InvalidReason = invalidReason,
            NewDeviceName = newDeviceName,
            InvalidatedAt = invalidatedAt
        });
    }

    /// <summary>
    /// Send heartbeat to keep session alive and check if session is still valid.
    /// Clients should call this every 30-60 seconds while active.
    /// </summary>
    /// <remarks>
    /// If `SessionValid` is false, the client should logout immediately.
    /// </remarks>
    [HttpPost("heartbeat")]
    [AllowAnonymous]
    public ActionResult<HeartbeatResponse> Heartbeat([FromBody] HeartbeatRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.SessionId) || string.IsNullOrWhiteSpace(request.DeviceId))
        {
            return BadRequest(new HeartbeatResponse
            {
                Success = false,
                SessionValid = false,
                Error = "SessionId and DeviceId are required"
            });
        }

        var (success, invalidReason) = _authService.Heartbeat(request.SessionId, request.DeviceId);

        return Ok(new HeartbeatResponse
        {
            Success = true,
            SessionValid = success,
            InvalidReason = invalidReason
        });
    }

    /// <summary>
    /// Force logout a user from all devices (Admin only).
    /// Use this to terminate a user's session remotely.
    /// </summary>
    [HttpPost("force-logout")]
    [Authorize(Roles = "Admin")]
    public ActionResult<ApiResponse> ForceLogout([FromBody] ForceLogoutRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.UserId))
        {
            return BadRequest(new ApiResponse
            {
                Success = false,
                Error = "UserId is required"
            });
        }

        var user = _userService.GetUserById(request.UserId);
        if (user == null)
        {
            return NotFound(new ApiResponse
            {
                Success = false,
                Error = "User not found"
            });
        }

        _authService.ForceLogoutUser(request.UserId, request.Reason);

        Logger.Information("Admin force logged out user: {Username}. Reason: {Reason}",
            user.Username, request.Reason ?? "No reason provided");

        return Ok(new ApiResponse
        {
            Success = true,
            Message = $"User {user.Username} has been logged out from all devices"
        });
    }

    /// <summary>
    /// Get all active sessions (Admin only).
    /// Returns a list of all currently active user sessions.
    /// </summary>
    [HttpGet("active-sessions")]
    [Authorize(Roles = "Admin")]
    public ActionResult<ActiveSessionsResponse> GetActiveSessions()
    {
        var sessions = _authService.GetAllActiveSessions();

        return Ok(new ActiveSessionsResponse
        {
            Success = true,
            Sessions = sessions.Select(s => new SessionInfo
            {
                SessionId = s.SessionId,
                UserId = s.UserId,
                Username = s.Username,
                DeviceId = s.DeviceId,
                DeviceName = s.DeviceName,
                LoginAt = s.LoginAt,
                LastActivityAt = s.LastActivityAt,
                IsActive = s.IsActive
            }).ToList()
        });
    }

    /// <summary>
    /// Get current user's session info
    /// </summary>
    [HttpGet("session")]
    [Authorize]
    public ActionResult<ApiResponse<SessionInfo>> GetCurrentSession()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new ApiResponse<SessionInfo>
            {
                Success = false,
                Error = "User not found"
            });
        }

        var session = _authService.GetUserSession(userId);
        if (session == null)
        {
            return NotFound(new ApiResponse<SessionInfo>
            {
                Success = false,
                Error = "No active session found"
            });
        }

        return Ok(new ApiResponse<SessionInfo>
        {
            Success = true,
            Data = new SessionInfo
            {
                SessionId = session.SessionId,
                UserId = session.UserId,
                Username = session.Username,
                DeviceId = session.DeviceId,
                DeviceName = session.DeviceName,
                LoginAt = session.LoginAt,
                LastActivityAt = session.LastActivityAt,
                IsActive = session.IsActive
            }
        });
    }

    #endregion
}
