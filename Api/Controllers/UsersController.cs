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
/// User management controller (Admin only)
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "Admin")]
public class UsersController : ControllerBase
{
    private readonly UserService _userService;
    private readonly AuthService _authService;
    private static ILogger Logger => Log.Logger;

    public UsersController(UserService userService, AuthService authService)
    {
        _userService = userService;
        _authService = authService;
    }

    /// <summary>
    /// Get all users
    /// </summary>
    [HttpGet]
    public ActionResult<ApiResponse<List<UserDto>>> GetAllUsers()
    {
        var users = _userService.GetAllUsers();
        var dtos = users.Select(u => new UserDto
        {
            Id = u.Id,
            Username = u.Username,
            DisplayName = u.DisplayName,
            Role = u.Role,
            IsActive = u.IsActive,
            CreatedAt = u.CreatedAt,
            LastLoginAt = u.LastLoginAt
        }).ToList();

        return Ok(new ApiResponse<List<UserDto>>
        {
            Success = true,
            Data = dtos
        });
    }

    /// <summary>
    /// Get user by ID
    /// </summary>
    [HttpGet("{id}")]
    public ActionResult<ApiResponse<UserDto>> GetUser(string id)
    {
        var user = _userService.GetUserById(id);
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
    /// Create new user
    /// </summary>
    [HttpPost]
    public ActionResult<ApiResponse<UserDto>> CreateUser([FromBody] CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Username))
        {
            return BadRequest(new ApiResponse<UserDto>
            {
                Success = false,
                Error = "Username is required"
            });
        }

        var (isValidPassword, passwordError) = PasswordValidator.Validate(request.Password);
        if (!isValidPassword)
        {
            return BadRequest(new ApiResponse<UserDto>
            {
                Success = false,
                Error = passwordError
            });
        }

        try
        {
            var user = _userService.CreateUser(
                request.Username,
                request.Password,
                request.DisplayName ?? request.Username,
                request.Role);

            return CreatedAtAction(nameof(GetUser), new { id = user.Id }, new ApiResponse<UserDto>
            {
                Success = true,
                Data = new UserDto
                {
                    Id = user.Id,
                    Username = user.Username,
                    DisplayName = user.DisplayName,
                    Role = user.Role,
                    IsActive = user.IsActive,
                    CreatedAt = user.CreatedAt
                }
            });
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(new ApiResponse<UserDto>
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Update user
    /// </summary>
    [HttpPut("{id}")]
    public ActionResult<ApiResponse<UserDto>> UpdateUser(string id, [FromBody] UpdateUserRequest request)
    {
        var user = _userService.UpdateUser(id, request.DisplayName, request.Role, request.IsActive);

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
    /// Delete user
    /// </summary>
    [HttpDelete("{id}")]
    public ActionResult<ApiResponse> DeleteUser(string id)
    {
        // Prevent self-deletion
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (id == currentUserId)
        {
            return BadRequest(new ApiResponse
            {
                Success = false,
                Error = "You cannot delete your own account"
            });
        }

        try
        {
            var success = _userService.DeleteUser(id);
            if (!success)
            {
                return NotFound(new ApiResponse
                {
                    Success = false,
                    Error = "User not found"
                });
            }

            // Revoke all tokens for this user
            _authService.LogoutUser(id);

            return Ok(new ApiResponse
            {
                Success = true,
                Message = "User deleted successfully"
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new ApiResponse
            {
                Success = false,
                Error = ex.Message
            });
        }
    }

    /// <summary>
    /// Reset user password
    /// </summary>
    [HttpPost("{id}/reset-password")]
    public ActionResult<ApiResponse> ResetPassword(string id, [FromBody] ResetPasswordRequest request)
    {
        var (isValidPassword, passwordError) = PasswordValidator.Validate(request.NewPassword);
        if (!isValidPassword)
        {
            return BadRequest(new ApiResponse
            {
                Success = false,
                Error = passwordError
            });
        }

        var success = _userService.ResetPassword(id, request.NewPassword);
        if (!success)
        {
            return NotFound(new ApiResponse
            {
                Success = false,
                Error = "User not found"
            });
        }

        // Revoke all tokens for this user
        _authService.LogoutUser(id);

        return Ok(new ApiResponse
        {
            Success = true,
            Message = "Password reset successfully"
        });
    }
}
