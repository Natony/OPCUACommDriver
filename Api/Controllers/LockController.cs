using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpcUaCommunicationEngine.Api.Models;
using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Services.Auth;
using Serilog;

namespace OpcUaCommunicationEngine.Api.Controllers;

/// <summary>
/// Operator lock management controller
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class LockController : ControllerBase
{
    private readonly OperatorLockService _lockService;
    private static ILogger Logger => Log.Logger;

    public LockController(OperatorLockService lockService)
    {
        _lockService = lockService;
    }

    /// <summary>
    /// Get current lock status
    /// </summary>
    [HttpGet("status")]
    public ActionResult<ApiResponse<LockStatusResponse>> GetStatus()
    {
        var status = _lockService.GetLockStatus();
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        var response = new LockStatusResponse
        {
            IsLocked = status.IsLocked,
            LockId = status.LockId,
            UserId = status.UserId,
            Username = status.Username,
            DisplayName = status.DisplayName,
            AcquiredAt = status.AcquiredAt,
            ExpiresAt = status.ExpiresAt,
            TimeRemainingSeconds = status.TimeRemainingSeconds,
            IsCurrentUser = status.UserId == currentUserId
        };

        return Ok(ApiResponse<LockStatusResponse>.Ok(response));
    }

    /// <summary>
    /// Acquire operator lock
    /// </summary>
    [HttpPost("acquire")]
    public ActionResult<AcquireLockResponse> AcquireLock([FromBody] AcquireLockRequest? request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var username = User.FindFirst(ClaimTypes.Name)?.Value;
        var displayName = User.FindFirst("displayName")?.Value ?? username;
        var roleString = User.FindFirst(ClaimTypes.Role)?.Value;
        var lockDurationStr = User.FindFirst("lockDurationMinutes")?.Value;

        if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(username))
        {
            return Unauthorized(new AcquireLockResponse
            {
                Success = false,
                Error = "User not authenticated"
            });
        }

        var role = Enum.TryParse<UserRole>(roleString, out var r) ? r : UserRole.Viewer;

        // Priority: request duration > user's configured duration > default
        int? durationMinutes = request?.DurationMinutes;
        if (durationMinutes == null && !string.IsNullOrEmpty(lockDurationStr) && int.TryParse(lockDurationStr, out var userDuration))
        {
            durationMinutes = userDuration;
        }

        var (success, operatorLock, error, lockedByUsername, lockedByDisplayName) =
            _lockService.TryAcquireLock(userId, username, displayName ?? username, role, durationMinutes);

        if (!success)
        {
            return Ok(new AcquireLockResponse
            {
                Success = false,
                Error = error,
                LockedByUsername = lockedByUsername,
                LockedByDisplayName = lockedByDisplayName
            });
        }

        return Ok(new AcquireLockResponse
        {
            Success = true,
            LockId = operatorLock!.LockId,
            ExpiresAt = operatorLock.ExpiresAt,
            TimeRemainingSeconds = (int)operatorLock.TimeRemaining.TotalSeconds
        });
    }

    /// <summary>
    /// Release operator lock
    /// </summary>
    [HttpPost("release")]
    public ActionResult<ApiResponse> ReleaseLock()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new ApiResponse
            {
                Success = false,
                Error = "User not authenticated"
            });
        }

        var (success, error) = _lockService.ReleaseLock(userId);

        if (!success)
        {
            return BadRequest(new ApiResponse
            {
                Success = false,
                Error = error
            });
        }

        return Ok(new ApiResponse
        {
            Success = true,
            Message = "Lock released successfully"
        });
    }

    /// <summary>
    /// Extend current lock
    /// </summary>
    [HttpPost("extend")]
    public ActionResult<AcquireLockResponse> ExtendLock([FromBody] ExtendLockRequest? request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new AcquireLockResponse
            {
                Success = false,
                Error = "User not authenticated"
            });
        }

        var durationMinutes = request?.DurationMinutes ?? 30;

        var (success, operatorLock, error) = _lockService.ExtendLock(userId, durationMinutes);

        if (!success)
        {
            return BadRequest(new AcquireLockResponse
            {
                Success = false,
                Error = error
            });
        }

        return Ok(new AcquireLockResponse
        {
            Success = true,
            LockId = operatorLock!.LockId,
            ExpiresAt = operatorLock.ExpiresAt,
            TimeRemainingSeconds = (int)operatorLock.TimeRemaining.TotalSeconds
        });
    }

    /// <summary>
    /// Force release lock (Admin only)
    /// </summary>
    [HttpPost("force-release")]
    [Authorize(Roles = "Admin")]
    public ActionResult<ApiResponse> ForceReleaseLock()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var roleString = User.FindFirst(ClaimTypes.Role)?.Value;

        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized(new ApiResponse
            {
                Success = false,
                Error = "User not authenticated"
            });
        }

        var role = Enum.TryParse<UserRole>(roleString, out var r) ? r : UserRole.Viewer;

        var (success, error) = _lockService.ForceReleaseLock(userId, role);

        if (!success)
        {
            return BadRequest(new ApiResponse
            {
                Success = false,
                Error = error
            });
        }

        return Ok(new ApiResponse
        {
            Success = true,
            Message = "Lock force released successfully"
        });
    }
}
