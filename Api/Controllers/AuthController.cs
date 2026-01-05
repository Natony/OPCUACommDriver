using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpcUaCommunicationEngine.Api.Models;
using OpcUaCommunicationEngine.Enums;
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
    /// Login with username and password
    /// </summary>
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

        var (accessToken, refreshToken, expiresAt, user) = _authService.Login(request.Username, request.Password);

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

        if (request.NewPassword.Length < 6)
        {
            return BadRequest(new ApiResponse
            {
                Success = false,
                Error = "New password must be at least 6 characters"
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
}
