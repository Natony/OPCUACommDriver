using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using OpcUaCommunicationEngine.Api.Models;
using OpcUaCommunicationEngine.Enums;
using OpcUaCommunicationEngine.Interfaces;
using OpcUaCommunicationEngine.Services.Auth;
using Serilog;

namespace OpcUaCommunicationEngine.Api.Hubs;

/// <summary>
/// SignalR Hub for real-time PLC data updates
/// </summary>
[Authorize]
public class PlcHub : Hub
{
    private readonly IPlcManager _plcManager;
    private readonly OperatorLockService _lockService;
    private readonly ILogger _logger;

    public PlcHub(IPlcManager plcManager, OperatorLockService lockService, ILogger logger)
    {
        _plcManager = plcManager;
        _lockService = lockService;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var username = Context.User?.FindFirst(ClaimTypes.Name)?.Value;

        _logger.Information("Client connected to PlcHub: {ConnectionId}, User: {Username}",
            Context.ConnectionId, username ?? "Unknown");

        // Send current status to newly connected client
        await SendCurrentStatus();

        // Send current lock status
        await SendLockStatus();

        await base.OnConnectedAsync();
    }

    public override Task OnDisconnectedAsync(Exception? exception)
    {
        var username = Context.User?.FindFirst(ClaimTypes.Name)?.Value;
        _logger.Information("Client disconnected from PlcHub: {ConnectionId}, User: {Username}",
            Context.ConnectionId, username ?? "Unknown");
        return base.OnDisconnectedAsync(exception);
    }

    /// <summary>
    /// Subscribe to specific PLC updates
    /// </summary>
    public async Task SubscribeToPlc(string plcId)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, $"plc:{plcId}");
        _logger.Debug("Client {ConnectionId} subscribed to PLC {PlcId}", Context.ConnectionId, plcId);
    }

    /// <summary>
    /// Unsubscribe from specific PLC updates
    /// </summary>
    public async Task UnsubscribeFromPlc(string plcId)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"plc:{plcId}");
        _logger.Debug("Client {ConnectionId} unsubscribed from PLC {PlcId}", Context.ConnectionId, plcId);
    }

    /// <summary>
    /// Subscribe to all PLC updates
    /// </summary>
    public async Task SubscribeToAll()
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, "all");
        _logger.Debug("Client {ConnectionId} subscribed to all PLCs", Context.ConnectionId);
    }

    /// <summary>
    /// Get current status of all PLCs
    /// </summary>
    public async Task GetStatus()
    {
        await SendCurrentStatus();
    }

    /// <summary>
    /// Get current lock status
    /// </summary>
    public async Task GetLockStatus()
    {
        await SendLockStatus();
    }

    /// <summary>
    /// Get all current tag values
    /// </summary>
    public async Task GetAllTagValues()
    {
        var tags = new List<TagDto>();

        foreach (var connection in _plcManager.Connections)
        {
            foreach (var tag in connection.Device.Tags)
            {
                tags.Add(new TagDto
                {
                    Id = tag.Id,
                    Name = tag.Name,
                    NodeId = tag.NodeId,
                    PlcId = connection.Device.Id,
                    PlcName = connection.Device.Name,
                    Value = tag.Value,
                    Quality = tag.Quality.ToString(),
                    Timestamp = tag.Timestamp,
                    DataType = tag.DataType.ToString(),
                    IsWritable = tag.CanWrite
                });
            }
        }

        await Clients.Caller.SendAsync("AllTagValues", tags);
    }

    /// <summary>
    /// Write a tag value (requires lock)
    /// </summary>
    public async Task WriteTag(string plcId, string nodeId, object value)
    {
        var userId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var roleString = Context.User?.FindFirst(ClaimTypes.Role)?.Value;

        // Validate user claims
        if (string.IsNullOrEmpty(userId))
        {
            await Clients.Caller.SendAsync("WriteResult", new
            {
                plcId,
                nodeId,
                success = false,
                error = "User not authenticated"
            });
            return;
        }

        if (!Enum.TryParse<UserRole>(roleString, out var role))
        {
            await Clients.Caller.SendAsync("WriteResult", new
            {
                plcId,
                nodeId,
                success = false,
                error = "Invalid or missing user role"
            });
            return;
        }

        try
        {
            // Check lock
            var (canWrite, error) = _lockService.CanUserWrite(userId, role);
            if (!canWrite)
            {
                await Clients.Caller.SendAsync("WriteResult", new
                {
                    plcId,
                    nodeId,
                    success = false,
                    error = error ?? "Lock required"
                });
                return;
            }

            var result = await _plcManager.WriteTagAsync(plcId, nodeId, value);

            if (result)
            {
                // Update lock activity to prevent timeout while actively writing
                _lockService.UpdateActivity(userId);
            }

            await Clients.Caller.SendAsync("WriteResult", new { plcId, nodeId, success = result });
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error writing tag via SignalR");
            await Clients.Caller.SendAsync("WriteResult", new { plcId, nodeId, success = false, error = ex.Message });
        }
    }

    private async Task SendCurrentStatus()
    {
        var statuses = _plcManager.Connections.Select(c => new ConnectionStatusDto
        {
            PlcId = c.Device.Id,
            PlcName = c.Device.Name,
            State = c.ConnectionState.ToString(),
            IsConnected = c.ConnectionState == PlcConnectionState.Connected
        }).ToList();

        await Clients.Caller.SendAsync("PlcStatus", statuses);
    }

    private async Task SendLockStatus()
    {
        var status = _lockService.GetLockStatus();
        var currentUserId = Context.User?.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        await Clients.Caller.SendAsync("LockStatus", new LockStatusResponse
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
        });
    }
}

/// <summary>
/// Service to broadcast updates to SignalR clients
/// </summary>
public class PlcHubService
{
    private readonly IHubContext<PlcHub> _hubContext;
    private readonly ILogger _logger;

    public PlcHubService(IHubContext<PlcHub> hubContext, ILogger logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    /// <summary>
    /// Broadcast tag value update to all subscribed clients
    /// </summary>
    public async Task BroadcastTagValueAsync(string plcId, string tagId, string nodeId, object? value, string quality, DateTime timestamp)
    {
        var update = new TagValueUpdate
        {
            PlcId = plcId,
            TagId = tagId,
            NodeId = nodeId,
            Value = value,
            Quality = quality,
            Timestamp = timestamp
        };

        // Send to clients subscribed to this specific PLC
        await _hubContext.Clients.Group($"plc:{plcId}").SendAsync("TagValueChanged", update);

        // Send to clients subscribed to all updates
        await _hubContext.Clients.Group("all").SendAsync("TagValueChanged", update);
    }

    /// <summary>
    /// Broadcast connection state change
    /// </summary>
    public async Task BroadcastConnectionStateAsync(string plcId, string plcName, string state, bool isConnected)
    {
        var status = new ConnectionStatusDto
        {
            PlcId = plcId,
            PlcName = plcName,
            State = state,
            IsConnected = isConnected,
            LastStateChange = DateTime.UtcNow
        };

        await _hubContext.Clients.All.SendAsync("ConnectionStateChanged", status);
    }

    /// <summary>
    /// Broadcast lock acquired event
    /// </summary>
    public async Task BroadcastLockAcquiredAsync(string userId, string username, string displayName, DateTime expiresAt)
    {
        await _hubContext.Clients.All.SendAsync("LockAcquired", new
        {
            UserId = userId,
            Username = username,
            DisplayName = displayName,
            ExpiresAt = expiresAt,
            TimeRemainingSeconds = (int)(expiresAt - DateTime.UtcNow).TotalSeconds
        });
    }

    /// <summary>
    /// Broadcast lock released event
    /// </summary>
    public async Task BroadcastLockReleasedAsync(string releasedByUsername, string reason)
    {
        await _hubContext.Clients.All.SendAsync("LockReleased", new
        {
            ReleasedBy = releasedByUsername,
            Reason = reason, // manual, timeout, forced
            Timestamp = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Broadcast lock extended event
    /// </summary>
    public async Task BroadcastLockExtendedAsync(string userId, DateTime newExpiresAt)
    {
        await _hubContext.Clients.All.SendAsync("LockExtended", new
        {
            UserId = userId,
            ExpiresAt = newExpiresAt,
            TimeRemainingSeconds = (int)(newExpiresAt - DateTime.UtcNow).TotalSeconds
        });
    }
}
